using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ClrDebug;
using DevStudio.Build;

namespace DevStudio.Debugger
{
    public sealed class BreakpointSpec
    {
        public string File { get; set; }
        public int Line { get; set; }
    }

    public sealed class DebugStackFrame
    {
        public string Method { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
        public int MethodToken { get; set; }
        public int ILOffset { get; set; }
    }

    public sealed class DebugVariable
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string TypeName { get; set; }
    }

    public sealed class DebugStopInfo
    {
        public string Reason { get; set; }
        public string ExceptionMessage { get; set; }
        public bool IsStep { get; set; }
        public List<DebugStackFrame> CallStack { get; } = new List<DebugStackFrame>();
        public List<DebugVariable> Locals { get; } = new List<DebugVariable>();
        public int ThreadId { get; set; }
    }

    /// <summary>Managed debugger for C# programs built with the Roslyn compiler,
    /// implemented on the native ICorDebug API (via ClrDebug).</summary>
    public sealed class ManagedDebugger : IDisposable
    {
        private readonly object _gate = new object();
        private BlockingCollection<Action> _queue;
        private Thread _debugThread;
        private CorDebug _corDebug;
        private CorDebugProcess _process;
        private CallbackHandler _handler;
        private SequencePointMap _map;
        private readonly List<BreakpointSpec> _pendingBreakpoints = new List<BreakpointSpec>();
        private readonly Dictionary<string, List<BoundBp>> _boundBreakpoints = new Dictionary<string, List<BoundBp>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<CorDebugStepper> _steppers = new List<CorDebugStepper>();
        private volatile bool _stopped;
        private int _stopCount;
        private int _processId;
        private IntPtr _processHandle = IntPtr.Zero;
        private CorDebugThread _stoppedThread;
        private string _exePath;
        private CorDebugModule _exeModule;
        private bool _stepping;
        private bool _stepIn;
        private string _stepFile;
        private int _stepLine = -1;
        private int _stepCount;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        public event Action<string> Output;
        public event Action<DebugStopInfo> Stopped;
        public event Action Resumed;
        public event Action ProcessExited;

        public bool IsRunning { get; private set; }
        public bool IsStepping { get { return _stepping; } }
        public bool IsStopped
        {
            get { return _stopped; }
        }

        private void EnsureDebuggerThread()
        {
            if (_debugThread != null)
            {
                return;
            }
            _queue = new BlockingCollection<Action>();
            _debugThread = new Thread(() =>
            {
                foreach (var action in _queue.GetConsumingEnumerable())
                {
                    action();
                }
            });
            _debugThread.SetApartmentState(ApartmentState.MTA);
            _debugThread.IsBackground = true;
            _debugThread.Start();
        }

        private void RunSync(Action action)
        {
            EnsureDebuggerThread();
            Exception error = null;
            using (var done = new ManualResetEventSlim(false))
            {
                _queue.Add(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        done.Set();
                    }
                });
                done.Wait();
            }
            if (error != null)
            {
                throw new InvalidOperationException(error.Message, error);
            }
        }

        public void Start(string exePath, IEnumerable<BreakpointSpec> breakpoints)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("A debug session is already running.");
            }

            var pdbPath = Path.ChangeExtension(exePath, ".pdb");
            if (!File.Exists(pdbPath))
            {
                Output?.Invoke("Warning: no PDB found, breakpoints and source mapping will be unavailable.");
            }
            _exePath = exePath;
            _map = new SequencePointMap(exePath);

            lock (_gate)
            {
                _pendingBreakpoints.Clear();
                _boundBreakpoints.Clear();
                _steppers.Clear();
                _debuggerErrorCount = 0;
                _pendingBreakpoints.AddRange(breakpoints ?? Enumerable.Empty<BreakpointSpec>());

                RunSync(() =>
                {
                    var corDebug = new CorDebug();
                    corDebug.Initialize();
                    _handler = new CallbackHandler(this);
                    corDebug.SetManagedHandler(_handler);

                    var startupInfo = default(STARTUPINFOW);
                    startupInfo.cb = Marshal.SizeOf(typeof(STARTUPINFOW));

                    var processInfo = new PROCESS_INFORMATION();
                    var process = corDebug.CreateProcess(
                        exePath,
                        "\"" + exePath + "\"",
                        default(SECURITY_ATTRIBUTES),
                        default(SECURITY_ATTRIBUTES),
                        false,
                        (CreateProcessFlags)0,
                        IntPtr.Zero,
                        Path.GetDirectoryName(exePath),
                        startupInfo,
                        ref processInfo,
                        CorDebugCreateProcessFlags.DEBUG_NO_SPECIAL_OPTIONS);

                    _corDebug = corDebug;
                    _process = process;
                    _processId = (int)processInfo.dwProcessId;
                    _processHandle = processInfo.hProcess;
                    try { CloseHandle(processInfo.hThread); } catch { }
                    Output?.Invoke("Debugging started (PID " + _processId + ").");
                });

                IsRunning = true;
                _stopped = false;
                _stopCount = 0;
            }
        }

        public void Stop()
        {
            bool killed = false;
            try
            {
                RunSync(() =>
                {
                    lock (_gate)
                    {
                        if (_process != null)
                        {
                            if (_stopped)
                            {
                                try { _process.Continue(false); } catch { }
                                _stopped = false;
                            }
                            try { _process.Terminate(0); } catch { }
                            KillNative();
                            EndSession();
                            killed = true;
                        }
                    }
                });
            }
            catch
            {
            }
            if (killed)
            {
                Output?.Invoke("Debugging stopped.");
                ProcessExited?.Invoke();
            }
        }

        private void KillNative()
        {
            try
            {
                if (_processHandle != IntPtr.Zero)
                {
                    TerminateProcess(_processHandle, 0);
                    WaitForSingleObject(_processHandle, 3000);
                    return;
                }
                if (_processId > 0)
                {
                    var proc = System.Diagnostics.Process.GetProcessById(_processId);
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
            }
            catch
            {
            }
        }

        private void EndSession()
        {
            try { _map?.Dispose(); } catch { }
            _map = null;
            try { _corDebug?.Terminate(); } catch { }
            _corDebug = null;
            _process = null;
            if (_processHandle != IntPtr.Zero)
            {
                try { CloseHandle(_processHandle); } catch { }
                _processHandle = IntPtr.Zero;
            }
            _stopped = false;
            _stoppedThread = null;
            _stepping = false;
            _exeModule = null;
            IsRunning = false;
            _steppers.Clear();
            _boundBreakpoints.Clear();
            _pendingBreakpoints.Clear();
        }

        public void Continue()
        {
            try
            {
                RunSync(() =>
                {
                    lock (_gate)
                    {
                        if (_process == null || !_stopped)
                        {
                            return;
                        }
                        foreach (var stepper in _steppers)
                        {
                            try { stepper.Deactivate(); } catch { }
                        }
                        _steppers.Clear();
                        _stopped = false;
                        _stopCount++;
                        _stoppedThread = null;
                        _stepping = false;
                        try
                        {
                            _process.Continue(false);
                        }
                        catch
                        {
                        }
                        Resumed?.Invoke();
                    }
                });
            }
            catch
            {
            }
        }

        public void Step(bool stepIn)
        {
            try
            {
                RunSync(() =>
                {
                    lock (_gate)
                    {
                        if (_process == null || !_stopped)
                        {
                            return;
                        }
                        var thread = _stoppedThread ?? FindStoppedThread();
                        if (thread == null)
                        {
                            return;
                        }
                        foreach (var stepper in _steppers)
                        {
                            try { stepper.Deactivate(); } catch { }
                        }
                        _steppers.Clear();
                        try
                        {
                            RecordStepStart(thread);
                            _stepIn = stepIn;
                            _stepping = true;
                            _stepCount = 0;
                            var stepper = thread.CreateStepper();
                            stepper.SetUnmappedStopMask(CorDebugUnmappedStop.STOP_NONE);
                            stepper.SetInterceptMask(CorDebugIntercept.INTERCEPT_NONE);
                            stepper.Step(stepIn);
                            _steppers.Add(stepper);
                            _stopped = false;
                            _stopCount++;
                            _process.Continue(false);
                            Resumed?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Output?.Invoke("Step failed: " + ex.Message);
                        }
                    }
                });
            }
            catch
            {
            }
        }

        public void StepOut()
        {
            try
            {
                RunSync(() =>
                {
                    lock (_gate)
                    {
                        if (_process == null || !_stopped)
                        {
                            return;
                        }
                        var thread = _stoppedThread ?? FindStoppedThread();
                        if (thread == null)
                        {
                            return;
                        }
                        try
                        {
                            var stepper = thread.CreateStepper();
                            stepper.StepOut();
                            _steppers.Add(stepper);
                            _stopped = false;
                            _stopCount++;
                            _process.Continue(false);
                            Resumed?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Output?.Invoke("Step out failed: " + ex.Message);
                        }
                    }
                });
            }
            catch
            {
            }
        }

        public DebugStopInfo CaptureStopInfo(string reason, CorDebugThread eventThread = null)
        {
            var info = new DebugStopInfo { Reason = reason };
            lock (_gate)
            {
                var thread = eventThread ?? FindStoppedThread();
                if (thread == null)
                {
                    return info;
                }
                info.ThreadId = thread.Id;
                _stoppedThread = thread;
                var chain = thread.ActiveChain;
                if (chain == null)
                {
                    return info;
                }

                CorDebugFrame[] frames;
                try
                {
                    frames = chain.Frames;
                }
                catch
                {
                    frames = new CorDebugFrame[0];
                }

                foreach (var frame in frames)
                {
                    try
                    {
                        var stackFrame = new DebugStackFrame();
                        var function = frame.Function;
                        if (function != null)
                        {
                            stackFrame.MethodToken = function.Token;
                            stackFrame.Method = _map != null ? _map.GetMethodName(function.Token) : "0x" + ((int)function.Token).ToString("X");
                            var module = function.Module;
                            var modulePath = module != null ? module.Name : null;
                            int ilOffset = -1;
                            var ilFrame = frame as CorDebugILFrame;
                            if (ilFrame != null)
                            {
                                try { ilOffset = ilFrame.IP.pnOffset; } catch { }
                            }
                            stackFrame.ILOffset = ilOffset;
                            string file;
                            int line;
                            if (ilOffset >= 0 && _map != null && modulePath != null && _map.TryGetSourceLine(modulePath, function.Token, ilOffset, out file, out line))
                            {
                                stackFrame.File = file;
                                stackFrame.Line = line;
                            }
                        }
                        else
                        {
                            stackFrame.Method = "<native frame>";
                        }
                        info.CallStack.Add(stackFrame);
                    }
                    catch
                    {
                    }
                    if (info.CallStack.Count >= 100)
                    {
                        break;
                    }
                }

                var activeFrame = frames.Length > 0 ? frames[0] : null;
                var activeIlFrame = activeFrame as CorDebugILFrame;
                if (activeIlFrame != null)
                {
                    int activeToken = 0;
                    try { activeToken = activeFrame.Function?.Token ?? 0; } catch { }
                    try
                    {
                        var locals = activeIlFrame.LocalVariables;
                        int index = 0;
                        foreach (var value in locals)
                        {
                            string name;
                            if (_map == null || !_map.TryGetLocalName(activeToken, index, out name))
                            {
                                name = "local[" + index + "]";
                            }
                            info.Locals.Add(FormatValue(name, value));
                            index++;
                        }
                        try
                        {
                            var arguments = activeIlFrame.Arguments;
                            int argIndex = 0;
                            foreach (var value in arguments)
                            {
                                info.Locals.Add(FormatValue("arg[" + argIndex + "]", value));
                                argIndex++;
                            }
                        }
                        catch
                        {
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return info;
        }

        private CorDebugThread FindStoppedThread()
        {
            if (_process == null)
            {
                return null;
            }
            try
            {
                foreach (var thread in _process.EnumerateThreads())
                {
                    var chain = thread.ActiveChain;
                    if (chain != null && chain.ActiveFrame != null)
                    {
                        return thread;
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private DebugVariable FormatValue(string name, CorDebugValue value)
        {
            var variable = new DebugVariable { Name = name, Value = "?", TypeName = value.Type.ToString() };
            try
            {
                var elementType = value.Type;
                switch (elementType)
                {
                    case CorElementType.Boolean:
                    {
                        var generic = value.As<CorDebugGenericValue>();
                        variable.Value = ReadPrimitive(generic, 1, r => Marshal.ReadByte(r) != 0).ToString();
                        break;
                    }
                    case CorElementType.I1:
                    {
                        var generic = value.As<CorDebugGenericValue>();
                        variable.Value = ReadPrimitive(generic, 1, r => (sbyte)Marshal.ReadByte(r)).ToString();
                        break;
                    }
                    case CorElementType.U1:
                    {
                        var generic = value.As<CorDebugGenericValue>();
                        variable.Value = ReadPrimitive(generic, 1, r => Marshal.ReadByte(r)).ToString();
                        break;
                    }
                    case CorElementType.I2:
                    {
                        var generic = value.As<CorDebugGenericValue>();
                        variable.Value = ReadPrimitive(generic, 2, Marshal.ReadInt16).ToString();
                        break;
                    }
                    case CorElementType.U2:
                    case CorElementType.Char:
                    {
                        var generic = value.As<CorDebugGenericValue>();
                        var raw = ReadPrimitive(generic, 2, Marshal.ReadInt16);
                        variable.Value = elementType == CorElementType.Char ? "'" + (char)raw + "'" : raw.ToString();
                        break;
                    }
                    case CorElementType.I4:
                    {
                        var generic = value.As<CorDebugGenericValue>();
                        variable.Value = ReadPrimitive(generic, 4, Marshal.ReadInt32).ToString();
                        break;
                    }
                    case CorElementType.U4:
                    {
                        var generic = value.As<CorDebugGenericValue>();
                        variable.Value = ReadPrimitive(generic, 4, Marshal.ReadInt32).ToString();
                        break;
                    }
                    case CorElementType.I8:
                    {
                        var generic = value.As<CorDebugGenericValue>();
                        variable.Value = ReadPrimitive(generic, 8, r => Marshal.PtrToStructure<double>(r)).ToString();
                        break;
                    }
                    case CorElementType.U8:
                    {
                        var generic = value.As<CorDebugGenericValue>();
                        variable.Value = ReadPrimitive(generic, 8, r => Marshal.PtrToStructure<double>(r)).ToString();
                        break;
                    }
                    case CorElementType.R4:
                    {
                        var generic = value.As<CorDebugGenericValue>();
                        variable.Value = ReadPrimitive(generic, 4, r => Marshal.PtrToStructure<float>(r)).ToString();
                        break;
                    }
                    case CorElementType.R8:
                    {
                        var generic = value.As<CorDebugGenericValue>();
                        variable.Value = ReadPrimitive(generic, 8, r => Marshal.PtrToStructure<double>(r)).ToString();
                        break;
                    }
                    case CorElementType.String:
                    {
                        var stringValue = value.As<CorDebugStringValue>();
                        int length = Math.Min(stringValue.Length, 512);
                        variable.Value = "\"" + stringValue.GetString(length) + "\"";
                        variable.TypeName = "string";
                        break;
                    }
                    case CorElementType.SZArray:
                    case CorElementType.Array:
                    {
                        var reference = value.As<CorDebugReferenceValue>();
                        var heap = reference.Dereference();
                        var array = heap != null ? heap.As<CorDebugArrayValue>() : null;
                        variable.Value = array != null ? "array, length " + array.Count : "array";
                        variable.TypeName = elementType.ToString();
                        break;
                    }
                    case CorElementType.Class:
                    case CorElementType.Object:
                    case CorElementType.ValueType:
                    case CorElementType.GenericInst:
                    {
                        var reference = value.As<CorDebugReferenceValue>();
                        bool isNull;
                        try { isNull = reference.IsNull; } catch { isNull = false; }
                        if (isNull)
                        {
                            variable.Value = "null";
                        }
                        else
                        {
                            variable.Value = "{object}";
                            try
                            {
                                var exactType = value.ExactType;
                                if (exactType != null && exactType.Class != null)
                                {
                                    variable.TypeName = "class (token " + exactType.Class.Token + ")";
                                }
                            }
                            catch
                            {
                            }
                        }
                        break;
                    }
                    default:
                        variable.Value = "(" + elementType + ")";
                        break;
                }
            }
            catch (Exception ex)
            {
                variable.Value = "<unable to read: " + ex.Message + ">";
            }
            return variable;
        }

        private T ReadPrimitive<T>(CorDebugGenericValue generic, int size, Func<IntPtr, T> read)
        {
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                generic.GetValue(buffer);
                return read(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private int _debuggerErrorCount;

        private void DispatchCallback(CorDebugManagedCallbackEventArgs e)
        {
            switch (e.Kind)
            {
                case CorDebugManagedCallbackKind.CreateProcess:
                    Output?.Invoke("Debuggee process created.");
                    Resume(e);
                    break;
                case CorDebugManagedCallbackKind.LoadModule:
                    {
                        var loadModule = (LoadModuleCorDebugManagedCallbackEventArgs)e;
                        OnLoadModule(loadModule.AppDomain, loadModule.Module);
                        Resume(e);
                    }
                    break;
                case CorDebugManagedCallbackKind.Breakpoint:
                    {
                        var bp = (BreakpointCorDebugManagedCallbackEventArgs)e;
                        _stepping = false;
                        e.Continue = false;
                        OnBreakpointHit(bp.Breakpoint, bp.Thread);
                    }
                    break;
                case CorDebugManagedCallbackKind.StepComplete:
                    {
                        var step = (StepCompleteCorDebugManagedCallbackEventArgs)e;
                        if (_stepping && ContinueSourceLineStep(step.Thread))
                        {
                            Resume(e);
                        }
                        else
                        {
                            bool wasStepping = _stepping;
                            _stepping = false;
                            e.Continue = false;
                            RaiseStop("Step complete", null, step.Thread, wasStepping);
                        }
                    }
                    break;
                case CorDebugManagedCallbackKind.Break:
                    _stepping = false;
                    e.Continue = false;
                    RaiseStop("Break", null, ((BreakCorDebugManagedCallbackEventArgs)e).Thread);
                    break;
                case CorDebugManagedCallbackKind.Exception:
                    HandleException(e, ((ExceptionCorDebugManagedCallbackEventArgs)e).Unhandled != 0);
                    break;
                case CorDebugManagedCallbackKind.Exception2:
                    HandleException(e, ((Exception2CorDebugManagedCallbackEventArgs)e).EventType == CorDebugExceptionCallbackType.DEBUG_EXCEPTION_UNHANDLED);
                    break;
                case CorDebugManagedCallbackKind.ExitProcess:
                    e.Continue = true;
                    Resume(e);
                    EndSession();
                    Output?.Invoke("Debuggee process exited.");
                    ProcessExited?.Invoke();
                    break;
                case CorDebugManagedCallbackKind.DebuggerError:
                    {
                        var error = (DebuggerErrorCorDebugManagedCallbackEventArgs)e;
                        Output?.Invoke("Debugger error in debuggee: " + DescribeDebuggerError(error.ErrorHR));
                        _debuggerErrorCount++;
                        if (_debuggerErrorCount > 50)
                        {
                            Output?.Invoke("Too many debugger errors; terminating debuggee.");
                            try { _process?.Terminate(1); } catch { }
                            IsRunning = false;
                            ProcessExited?.Invoke();
                        }
                        else
                        {
                            Resume(e);
                        }
                    }
                    break;
                case CorDebugManagedCallbackKind.BreakpointSetError:
                    {
                        var bse = (BreakpointSetErrorCorDebugManagedCallbackEventArgs)e;
                        Output?.Invoke("Breakpoint could not be set in the debuggee (error " + bse.Error + ").");
                        Resume(e);
                    }
                    break;
                case CorDebugManagedCallbackKind.LogMessage:
                    Output?.Invoke("[log] " + ((LogMessageCorDebugManagedCallbackEventArgs)e).Message);
                    Resume(e);
                    break;
                default:
                    Resume(e);
                    break;
            }
        }

        private void RecordStepStart(CorDebugThread thread)
        {
            _stepFile = null;
            _stepLine = -1;
            try
            {
                var frame = thread.ActiveFrame;
                if (frame == null || frame.Function == null)
                {
                    return;
                }
                int token = frame.Function.Token;
                int offset = -1;
                var ilFrame = frame as CorDebugILFrame;
                if (ilFrame != null)
                {
                    try { offset = ilFrame.IP.pnOffset; } catch { }
                }
                string file;
                int line;
                if (offset >= 0 && _map != null && _map.TryGetSourceLine(null, token, offset, out file, out line))
                {
                    _stepFile = file;
                    _stepLine = line;
                }
            }
            catch
            {
            }
        }

        private bool ContinueSourceLineStep(CorDebugThread thread)
        {
            if (_stepCount >= 1000)
            {
                return false;
            }
            try
            {
                var frame = thread.ActiveFrame;
                if (frame == null || frame.Function == null)
                {
                    return false;
                }
                int token = frame.Function.Token;
                int offset = -1;
                var ilFrame = frame as CorDebugILFrame;
                if (ilFrame != null)
                {
                    try { offset = ilFrame.IP.pnOffset; } catch { }
                }
                string file;
                int line;
                if (offset >= 0 && _stepLine > 0 && _map != null && _map.TryGetSourceLine(null, token, offset, out file, out line) &&
                    line == _stepLine && string.Equals(file, _stepFile, StringComparison.OrdinalIgnoreCase))
                {
                    _stepCount++;
                    foreach (var old in _steppers)
                    {
                        try { old.Deactivate(); } catch { }
                    }
                    _steppers.Clear();
                    var stepper = thread.CreateStepper();
                    stepper.SetUnmappedStopMask(CorDebugUnmappedStop.STOP_NONE);
                    stepper.SetInterceptMask(CorDebugIntercept.INTERCEPT_NONE);
                    stepper.Step(_stepIn);
                    _steppers.Add(stepper);
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private void HandleException(CorDebugManagedCallbackEventArgs e, bool unhandled)
        {
            if (unhandled)
            {
                _stepping = false;
                e.Continue = false;
                var thread = e is ExceptionCorDebugManagedCallbackEventArgs
                    ? ((ExceptionCorDebugManagedCallbackEventArgs)e).Thread
                    : ((Exception2CorDebugManagedCallbackEventArgs)e).Thread;
                RaiseStop("Unhandled exception", "An unhandled exception occurred in the debuggee.", thread);
            }
            else
            {
                Output?.Invoke("First-chance exception in debuggee.");
                Resume(e);
            }
        }

        private void Resume(CorDebugManagedCallbackEventArgs e)
        {
            e.Continue = true;
            try
            {
                e.Controller?.Continue(false);
            }
            catch
            {
            }
        }

        public void AddBreakpoint(string file, int line)
        {
            try
            {
                RunSync(() =>
                {
                    lock (_gate)
                    {
                        if (_process == null || _map == null || _exeModule == null)
                        {
                            return;
                        }
                        var key = file + ":" + line;
                        if (_boundBreakpoints.ContainsKey(key))
                        {
                            return;
                        }
                        BreakpointBinding binding;
                        if (!_map.TryBind(file, line, out binding))
                        {
                            Output?.Invoke("Breakpoint will not be hit: " + Path.GetFileName(file) + ":" + line +
                                " (no executable code found for this line)");
                            return;
                        }
                        try
                        {
                            var function = _exeModule.GetFunctionFromToken(binding.MethodToken);
                            var bp = function.ILCode.CreateBreakpoint(binding.ILOffset);
                            List<BoundBp> list;
                            if (!_boundBreakpoints.TryGetValue(key, out list))
                            {
                                list = new List<BoundBp>();
                                _boundBreakpoints[key] = list;
                            }
                            list.Add(new BoundBp { Breakpoint = bp, ModulePath = _exePath, Token = binding.MethodToken, Offset = binding.ILOffset });
                            Output?.Invoke("Breakpoint bound: " + Path.GetFileName(file) + ":" + line);
                        }
                        catch (Exception ex)
                        {
                            Output?.Invoke("Breakpoint could not be bound: " + Path.GetFileName(file) + ":" + line + " (" + ex.Message + ")");
                        }
                    }
                });
            }
            catch
            {
            }
        }

        public void RemoveBreakpoint(string file, int line)
        {
            try
            {
                RunSync(() =>
                {
                    lock (_gate)
                    {
                        var key = file + ":" + line;
                        List<BoundBp> list;
                        if (_boundBreakpoints.TryGetValue(key, out list))
                        {
                            foreach (var bound in list)
                            {
                                try { bound.Breakpoint.Activate(false); } catch { }
                            }
                            _boundBreakpoints.Remove(key);
                        }
                    }
                });
            }
            catch
            {
            }
        }

        private void OnLoadModule(CorDebugAppDomain appDomain, CorDebugModule module)
        {
            try
            {
                var modulePath = module.Name;
                if (_exePath != null &&
                    !string.Equals(Path.GetFileName(modulePath), Path.GetFileName(_exePath), StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                _exeModule = module;
                List<BreakpointSpec> toBind;
                lock (_gate)
                {
                    toBind = _pendingBreakpoints.ToList();
                }
                if (toBind.Count == 0)
                {
                    return;
                }

                foreach (var spec in toBind)
                {
                    BreakpointBinding binding;
                    if (_map == null || !_map.TryBind(spec.File, spec.Line, out binding))
                    {
                        continue;
                    }
                    try
                    {
                        var function = module.GetFunctionFromToken(binding.MethodToken);
                        var bp = function.ILCode.CreateBreakpoint(binding.ILOffset);
                        var key = spec.File + ":" + spec.Line;
                        lock (_gate)
                        {
                            List<BoundBp> list;
                            if (!_boundBreakpoints.TryGetValue(key, out list))
                            {
                                list = new List<BoundBp>();
                                _boundBreakpoints[key] = list;
                            }
                            list.Add(new BoundBp { Breakpoint = bp, ModulePath = modulePath, Token = binding.MethodToken, Offset = binding.ILOffset });
                            _pendingBreakpoints.RemoveAll(p => p.File == spec.File && p.Line == spec.Line);
                        }
                        Output?.Invoke("Breakpoint bound: " + Path.GetFileName(spec.File) + ":" + spec.Line);
                    }
                    catch
                    {
                    }
                }

                List<BreakpointSpec> unbound;
                lock (_gate)
                {
                    unbound = _pendingBreakpoints.ToList();
                }
                foreach (var spec in unbound)
                {
                    Output?.Invoke("Breakpoint will not be hit: " + Path.GetFileName(spec.File) + ":" + spec.Line +
                        " (no executable code was found for this line in " + Path.GetFileName(modulePath) + ")");
                    _pendingBreakpoints.Remove(spec);
                }
            }
            catch
            {
            }
        }

        private void OnBreakpointHit(CorDebugBreakpoint breakpoint, CorDebugThread eventThread)
        {
            var functionBreakpoint = breakpoint as CorDebugFunctionBreakpoint;
            string reason = "Breakpoint";
            if (functionBreakpoint != null)
            {
                try
                {
                    var function = functionBreakpoint.Function;
                    var module = function.Module;
                    var modulePath = module.Name;
                    string file;
                    int line;
                    if (_map != null && _map.TryGetSourceLine(modulePath, function.Token, functionBreakpoint.Offset, out file, out line))
                    {
                        reason = "Breakpoint at " + Path.GetFileName(file) + ":" + line;
                    }
                }
                catch
                {
                }
            }
            RaiseStop(reason, null, eventThread);
        }

        private void RaiseStop(string reason, string exceptionMessage, CorDebugThread eventThread = null, bool isStep = false)
        {
            _stopped = true;
            var info = CaptureStopInfo(reason, eventThread);
            info.ExceptionMessage = exceptionMessage;
            info.IsStep = isStep;
            Stopped?.Invoke(info);
        }

        public void Dispose()
        {
            Stop();
            try { _map?.Dispose(); } catch { }
        }

        private static string DescribeDebuggerError(HRESULT hr)
        {
            switch (hr)
            {
                case HRESULT.CORDBG_E_INTEROP_NOT_SUPPORTED: return "CORDBG_E_INTEROP_NOT_SUPPORTED: interop (mixed native/managed) debugging is not supported on this platform. Managed-only debugging continues; this error can usually be ignored.";
                case HRESULT.CORDBG_E_UNSUPPORTED_DEBUGGING_MODEL: return "CORDBG_E_UNSUPPORTED_DEBUGGING_MODEL: the debuggee runtime does not support this debugging model.";
                case HRESULT.CORDBG_E_DEBUGGING_NOT_POSSIBLE: return "CORDBG_E_DEBUGGING_NOT_POSSIBLE: debugging is not possible on this combination of runtimes.";
                default: return "HRESULT 0x" + ((int)hr).ToString("X8");
            }
        }

        private sealed class BoundBp
        {
            public CorDebugFunctionBreakpoint Breakpoint;
            public string ModulePath;
            public int Token;
            public int Offset;
        }

        private sealed class CallbackHandler : CorDebugManagedCallback
        {
            public CallbackHandler(ManagedDebugger owner)
            {
                OnAnyEvent += (s, e) => owner.DispatchCallback(e);
            }
        }
    }
}
