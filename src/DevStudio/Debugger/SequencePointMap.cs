using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace DevStudio.Debugger
{
    public struct BreakpointBinding
    {
        public int MethodToken;
        public int ILOffset;
        public string File;
        public int Line;
    }

    /// <summary>Maps source lines to (method token, IL offset) using the portable PDB
    /// emitted by the C# compiler, and resolves method names for the call stack.</summary>
    public sealed class SequencePointMap : IDisposable
    {
        private readonly Dictionary<string, List<BreakpointBinding>> _byLine = new Dictionary<string, List<BreakpointBinding>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<int, SortedList<int, int>>> _ilToLine = new Dictionary<string, Dictionary<int, SortedList<int, int>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, string> _methodNames = new Dictionary<int, string>();
        private readonly Dictionary<int, List<PdbLocalScope>> _localScopes = new Dictionary<int, List<PdbLocalScope>>();
        private readonly Dictionary<int, MethodArgumentInfo> _argumentInfo = new Dictionary<int, MethodArgumentInfo>();
        private MetadataReaderProvider _pdbProvider;
        private PEReader _peReader;
        private MetadataReader _pdbReader;
        private MetadataReader _peMetadataReader;

        public bool Loaded { get; private set; }

        private sealed class PdbLocalScope
        {
            public int StartOffset;
            public int Length;
            public Dictionary<int, string> Variables;
        }

        private sealed class MethodArgumentInfo
        {
            public bool IsStatic;
            public string[] ParameterNames;
        }

        public SequencePointMap(string exePath)
        {
            try
            {
                var pdbPath = Path.ChangeExtension(exePath, ".pdb");
                if (!File.Exists(pdbPath))
                {
                    return;
                }

                _pdbProvider = MetadataReaderProvider.FromPortablePdbStream(OpenShared(pdbPath));
                _pdbReader = _pdbProvider.GetMetadataReader();

                if (File.Exists(exePath))
                {
                    _peReader = new PEReader(OpenShared(exePath));
                    _peMetadataReader = _peReader.GetMetadataReader();
                    foreach (var methodHandle in _peMetadataReader.MethodDefinitions)
                    {
                        var method = _peMetadataReader.GetMethodDefinition(methodHandle);
                        var name = _peMetadataReader.GetString(method.Name);
                        var declaringType = _peMetadataReader.GetTypeDefinition(method.GetDeclaringType());
                        var typeName = _peMetadataReader.GetString(declaringType.Name);
                        var token = MetadataTokens.GetToken(methodHandle);
                        _methodNames[token] = typeName + "." + name;
                        var parameterNames = new List<string>();
                        foreach (var parameterHandle in method.GetParameters().OrderBy(p => _peMetadataReader.GetParameter(p).SequenceNumber))
                        {
                            var parameter = _peMetadataReader.GetParameter(parameterHandle);
                            if (parameter.SequenceNumber <= 0)
                            {
                                continue;
                            }
                            var parameterName = _peMetadataReader.GetString(parameter.Name);
                            if (!string.IsNullOrEmpty(parameterName))
                            {
                                parameterNames.Add(parameterName);
                            }
                        }
                        _argumentInfo[token] = new MethodArgumentInfo
                        {
                            IsStatic = (method.Attributes & System.Reflection.MethodAttributes.Static) != 0,
                            ParameterNames = parameterNames.ToArray()
                        };
                    }
                }

                for (int row = 1; row <= _pdbReader.MethodDebugInformation.Count; row++)
                {
                    var handle = MetadataTokens.MethodDebugInformationHandle(row);
                    var debugInfo = _pdbReader.GetMethodDebugInformation(handle);
                    if (debugInfo.Document.IsNil)
                    {
                        continue;
                    }
                    var methodToken = MetadataTokens.GetToken(MetadataTokens.MethodDefinitionHandle(row));
                    LoadLocalNames(methodToken, handle);
                    var points = debugInfo.GetSequencePoints().ToList();
                    if (points.Count == 0)
                    {
                        continue;
                    }

                    foreach (var point in points)
                    {
                        if (point.StartLine <= 0 || point.StartLine >= 0xFEEFEE || point.Document.IsNil)
                        {
                            continue;
                        }
                        var document = _pdbReader.GetDocument(point.Document);
                        var docName = _pdbReader.GetString(document.Name);
                        if (!_byLine.TryGetValue(docName, out var list))
                        {
                            list = new List<BreakpointBinding>();
                            _byLine[docName] = list;
                        }
                        list.Add(new BreakpointBinding
                        {
                            MethodToken = methodToken,
                            ILOffset = point.Offset,
                            File = docName,
                            Line = point.StartLine
                        });

                        if (!_ilToLine.TryGetValue(docName, out var perMethod))
                        {
                            perMethod = new Dictionary<int, SortedList<int, int>>();
                            _ilToLine[docName] = perMethod;
                        }
                        if (!perMethod.TryGetValue(methodToken, out var offsets))
                        {
                            offsets = new SortedList<int, int>();
                            perMethod[methodToken] = offsets;
                        }
                        if (!offsets.ContainsKey(point.Offset))
                        {
                            offsets.Add(point.Offset, point.StartLine);
                        }
                    }
                }

                Loaded = true;
            }
            catch
            {
                Loaded = false;
            }
        }

        private void LoadLocalNames(int methodToken, MethodDebugInformationHandle debugInfoHandle)
        {
            try
            {
                var scopes = new List<PdbLocalScope>();
                foreach (var scopeHandle in _pdbReader.GetLocalScopes(debugInfoHandle))
                {
                    var scope = _pdbReader.GetLocalScope(scopeHandle);
                    var variables = new Dictionary<int, string>();
                    foreach (var variableHandle in scope.GetLocalVariables())
                    {
                        var variable = _pdbReader.GetLocalVariable(variableHandle);
                        var name = _pdbReader.GetString(variable.Name);
                        if (!string.IsNullOrEmpty(name) && !variables.ContainsKey(variable.Index))
                        {
                            variables[variable.Index] = name;
                        }
                    }
                    scopes.Add(new PdbLocalScope
                    {
                        StartOffset = scope.StartOffset,
                        Length = scope.Length,
                        Variables = variables
                    });
                }
                if (scopes.Count > 0)
                {
                    _localScopes[methodToken] = scopes;
                }
            }
            catch
            {
            }
        }

        public bool HasLocalNames(int methodToken)
        {
            List<PdbLocalScope> scopes;
            if (!_localScopes.TryGetValue(methodToken, out scopes))
            {
                return false;
            }
            foreach (var scope in scopes)
            {
                if (scope.Variables.Count > 0)
                {
                    return true;
                }
            }
            return false;
        }

        public bool TryGetLocalName(int methodToken, int localIndex, int ilOffset, out string name)
        {
            name = null;
            List<PdbLocalScope> scopes;
            if (!_localScopes.TryGetValue(methodToken, out scopes))
            {
                return false;
            }
            foreach (var scope in scopes)
            {
                if (ilOffset >= 0 && (ilOffset < scope.StartOffset || ilOffset >= scope.StartOffset + scope.Length))
                {
                    continue;
                }
                if (scope.Variables.TryGetValue(localIndex, out name))
                {
                    return true;
                }
            }
            name = null;
            return false;
        }

        public bool TryGetArgumentName(int methodToken, int argIndex, out string name)
        {
            name = null;
            MethodArgumentInfo info;
            if (!_argumentInfo.TryGetValue(methodToken, out info))
            {
                return false;
            }
            if (!info.IsStatic && argIndex == 0)
            {
                name = "this";
                return true;
            }
            int parameterIndex = info.IsStatic ? argIndex : argIndex - 1;
            if (parameterIndex < 0 || parameterIndex >= info.ParameterNames.Length)
            {
                return false;
            }
            name = info.ParameterNames[parameterIndex];
            return true;
        }

        private static FileStream OpenShared(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        public bool TryBind(string file, int line, out BreakpointBinding binding)
        {
            binding = default(BreakpointBinding);
            List<BreakpointBinding> list;
            if (!_byLine.TryGetValue(file, out list))
            {
                foreach (var key in _byLine.Keys)
                {
                    if (string.Equals(Path.GetFileName(key), Path.GetFileName(file), StringComparison.OrdinalIgnoreCase))
                    {
                        list = _byLine[key];
                        break;
                    }
                }
            }
            if (list == null)
            {
                return false;
            }

            BreakpointBinding? best = list
                .Where(b => b.Line >= line)
                .OrderBy(b => b.Line)
                .Cast<BreakpointBinding?>()
                .FirstOrDefault();

            if (best == null)
            {
                best = list
                    .Where(b => b.Line <= line)
                    .OrderByDescending(b => b.Line)
                    .Cast<BreakpointBinding?>()
                    .FirstOrDefault();
            }

            if (best == null)
            {
                return false;
            }
            binding = best.Value;
            return true;
        }

        public bool TryGetSourceLine(string modulePath, int methodToken, int ilOffset, out string file, out int line)
        {
            file = null;
            line = 0;
            foreach (var kv in _ilToLine)
            {
                SortedList<int, int> offsets;
                if (!kv.Value.TryGetValue(methodToken, out offsets))
                {
                    continue;
                }
                int bestOffset = -1;
                foreach (var offset in offsets.Keys)
                {
                    if (offset <= ilOffset)
                    {
                        bestOffset = offset;
                    }
                    else
                    {
                        break;
                    }
                }
                if (bestOffset >= 0)
                {
                    file = kv.Key;
                    line = offsets[bestOffset];
                    return true;
                }
            }
            return false;
        }

        public string GetMethodName(int methodToken)
        {
            string name;
            return _methodNames.TryGetValue(methodToken, out name) ? name : "0x" + methodToken.ToString("X");
        }

        public void Dispose()
        {
            try { _pdbProvider?.Dispose(); } catch { }
            try { _peReader?.Dispose(); } catch { }
        }
    }
}
