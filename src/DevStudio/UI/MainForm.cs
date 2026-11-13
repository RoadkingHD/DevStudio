using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevStudio.Build;
using DevStudio.Debugger;
using DevStudio.Designer;
using DevStudio.Dialogs;
using DevStudio.Editor;
using DevStudio.Panels;
using DevStudio.Projects;
using DevStudio.Services;
using DevStudio.Theming;

namespace DevStudio.UI
{
    public partial class MainForm : Form
    {
        private sealed class EditorTab
        {
            public TabPage Page;
            public CodeEditorHost Editor;
            public EditorDocument Doc;
            public DesignerHostControl Designer;
            public TabControl InnerTabs;
            public TabPage CodePage;
            public TabPage DesignerPage;
        }

        private readonly List<EditorTab> _tabs = new List<EditorTab>();
        private readonly Dictionary<string, EditorTab> _tabsByPath = new Dictionary<string, EditorTab>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ProjectBase> _projects = new List<ProjectBase>();
        private readonly ManagedDebugger _debugger = new ManagedDebugger();
        private DebugStopInfo _lastStopInfo;
        private readonly NavigationService _navigation = new NavigationService();
        private readonly CSharpBuildService _csharpBuilder;
        private readonly CppBuildService _cppBuilder;
        private bool _building;
        private bool _minimapVisible = true;

        public MainForm(string[] args)
        {
            InitializeComponent();
            ApplyToolbarIcons();
            SetupEditorTabCloseButtons();
            _csharpBuilder = new CSharpBuildService(outputPanel.AppendLine);
            _cppBuilder = new CppBuildService(outputPanel.AppendLine);
            WireEvents();
            PopulateThemeMenu();
            SetupSnippetMenu();
            if (args != null)
            {
                foreach (var arg in args)
                {
                    OpenPath(arg);
                }
            }
            UiTheme.SetTheme(EditorTheme.Dark, this);
            UpdateDebugUi();
        }

        private void SetupEditorTabCloseButtons()
        {
            UiTheme.EnableTabCloseButtons(editorTabs);
            editorTabs.MouseUp += (s, e) =>
            {
                if (e.Button != MouseButtons.Left)
                {
                    return;
                }
                for (int i = 0; i < editorTabs.TabCount && i < _tabs.Count; i++)
                {
                    if (UiTheme.TabCloseRect(editorTabs.GetTabRect(i)).Contains(e.Location))
                    {
                        CloseTab(_tabs[i]);
                        break;
                    }
                }
            };
            var tabMenu = new ContextMenuStrip();
            var closeItem = new ToolStripMenuItem("Close");
            closeItem.Click += (s, e) =>
            {
                var page = tabMenu.SourceControl as TabPage;
                if (page == null && editorTabs.SelectedTab != null)
                {
                    page = editorTabs.SelectedTab;
                }
                EditorTab tab;
                if (page != null && _tabsByPath.TryGetValue(page.Name, out tab))
                {
                    CloseTab(tab);
                }
            };
            var closeAllItem = new ToolStripMenuItem("Close All Documents");
            closeAllItem.Click += (s, e) => CloseAllTabsClick(s, e);
            tabMenu.Items.Add(closeItem);
            tabMenu.Items.Add(closeAllItem);
            editorTabs.ContextMenuStrip = tabMenu;
        }

        private void CloseAllTabsClick(object sender, EventArgs e)
        {
            for (int i = _tabs.Count - 1; i >= 0; i--)
            {
                CloseTab(_tabs[i]);
            }
        }

        private void ApplyToolbarIcons()
        {
            newButton.Image = StudioIcons.Get("New");
            openButton.Image = StudioIcons.Get("Open");
            saveButton.Image = StudioIcons.Get("Save");
            buildButton.Image = StudioIcons.Get("Build");
            startButton.Image = StudioIcons.Get("Start");
            stopButton.Image = StudioIcons.Get("Stop");
            stepOverButton.Image = StudioIcons.Get("StepOver");
            stepIntoButton.Image = StudioIcons.Get("StepInto");
            stepOutButton.Image = StudioIcons.Get("StepOut");
            ToolStripItem[] items = { newButton, openButton, saveButton, buildButton, startButton, stopButton, stepOverButton, stepIntoButton, stepOutButton };
            foreach (var item in items)
            {
                item.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
            }
        }

        private void WireEvents()
        {
            solutionExplorer.FileOpenRequested += path => OpenFile(path);
            solutionExplorer.AddFileRequested += AddExistingFile;
            solutionExplorer.AddNewFileRequested += AddNewFile;
            solutionExplorer.AddReferenceRequested += ShowAddReference;
            solutionExplorer.FileRemoved += RemoveFileFromProject;
            solutionExplorer.ProjectRemoved += CloseProject;

            editorTabs.SelectedIndexChanged += (s, e) => { RefreshBreakpointsPanel(); UpdateCaretStatus(); };

            errorListPanel.ItemActivated += item => NavigateTo(item.File, item.Line, item.Column);
            findResultsPanel.Navigate += hit => NavigateTo(hit.File, hit.Line, hit.Column);
            callStackPanel.Navigate += frame => NavigateTo(frame.File, frame.Line, 1);
            breakpointsPanel.Navigate += (file, line) => NavigateTo(file, line, 1);
            breakpointsPanel.RemoveRequested += RemoveBreakpoint;

            _debugger.Output += line => SafeOutput(line);
            _debugger.Stopped += OnDebuggerStopped;
            _debugger.Resumed += OnDebuggerResumed;
            _debugger.ProcessExited += OnDebuggerExited;
        }

        private void PopulateThemeMenu()
        {
            foreach (var theme in EditorTheme.All)
            {
                var captured = theme;
                var item = new ToolStripMenuItem(theme.Name);
                item.Click += (s, e) =>
                {
                    UiTheme.SetTheme(captured, this);
                    foreach (var tab in _tabs)
                    {
                        tab.Editor.ApplyTheme(captured);
                    }
                };
                themeMenu.DropDownItems.Add(item);
            }
        }

        private void OpenPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".csproj" || extension == ".vcxproj")
            {
                OpenProject(path);
            }
            else if (extension == ".cs" || extension == ".cpp" || extension == ".cc" || extension == ".cxx" || extension == ".c" || extension == ".h" || extension == ".hpp")
            {
                OpenSingleFile(path);
            }
        }

        public void OpenFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }
            path = Path.GetFullPath(path);
            EditorTab existing;
            if (_tabsByPath.TryGetValue(path, out existing))
            {
                editorTabs.SelectedTab = existing.Page;
                return;
            }

            var doc = new EditorDocument(path);
            var host = new CodeEditorHost { Dock = DockStyle.Fill };
            host.LoadDocument(doc);
            host.ShowMinimap(_minimapVisible);

            var tab = new EditorTab { Doc = doc, Editor = host };
            host.TextChanged += (s, e) =>
            {
                doc.Dirty = true;
                UpdateCaretStatus();
            };
            host.CaretMoved += UpdateCaretStatus;
            host.BreakpointToggled += (s, line) =>
            {
                bool added = !doc.BreakpointLines.Remove(line);
                if (added)
                {
                    doc.BreakpointLines.Add(line);
                }
                RefreshBreakpointsPanel();
                if (_debugger.IsRunning)
                {
                    if (added)
                    {
                        _debugger.AddBreakpoint(doc.FilePath, line);
                    }
                    else
                    {
                        _debugger.RemoveBreakpoint(doc.FilePath, line);
                    }
                }
            };
            host.GoToDefinitionRequested += (s, e) => { var t = GoToDefinition(); };
            host.FindReferencesRequested += (s, e) => { var t = FindReferences(); };
            host.FindRequested += FindClick;
            host.DebugValueResolver = ResolveDebugValue;

            var page = new TabPage(doc.DisplayName) { Name = path };
            var designerPath = FindDesignerSource(path, doc.Language);
            bool hasDesigner = designerPath != null;
            if (hasDesigner)
            {
                var designer = new DesignerHostControl { Dock = DockStyle.Fill };
                UiTheme.Apply(designer);
                designer.DiagnosticOutput += message => outputPanel.AppendLine(message);
                tab.Designer = designer;
                tab.InnerTabs = new TabControl { Dock = DockStyle.Fill };
                tab.CodePage = new TabPage("Code") { Name = "Code" };
                tab.DesignerPage = new TabPage("Designer") { Name = "Designer" };
                tab.CodePage.Controls.Add(host);
                tab.DesignerPage.Controls.Add(designer);
                tab.InnerTabs.TabPages.Add(tab.CodePage);
                tab.InnerTabs.TabPages.Add(tab.DesignerPage);
                page.Controls.Add(tab.InnerTabs);
                designer.SourceChanged += text => WriteDesignerSource(designerPath, text);
                designer.EventHandlerNeeded += (handlerName, className) => InsertEventHandlerMethod(path, designerPath, handlerName, className);
                designer.ViewCodeRequested += () => { tab.InnerTabs.SelectedTab = tab.CodePage; };
                tab.InnerTabs.SelectedIndexChanged += (s, e) =>
                {
                    if (tab.InnerTabs.SelectedTab == tab.DesignerPage)
                    {
                        designer.LoadSource(GetDesignerSource(designerPath));
                    }
                };
                designer.LoadSource(GetDesignerSource(designerPath));
            }
            else
            {
                page.Controls.Add(host);
            }

            tab.Page = page;
            _tabs.Add(tab);
            _tabsByPath[path] = tab;
            editorTabs.TabPages.Add(page);
            editorTabs.SelectedTab = page;
        }

        private static string FindDesignerSource(string path, DevLanguage language)
        {
            if (language != DevLanguage.CSharp)
            {
                return null;
            }
            if (HasInitializeComponentDeclaration(File.ReadAllText(path)))
            {
                return path;
            }
            var sibling = Path.Combine(Path.GetDirectoryName(path),
                Path.GetFileNameWithoutExtension(path) + ".Designer.cs");
            if (File.Exists(sibling) &&
                HasInitializeComponentDeclaration(File.ReadAllText(sibling)))
            {
                return sibling;
            }
            return null;
        }

        private static bool HasInitializeComponentDeclaration(string source)
        {
            return source.IndexOf("void InitializeComponent", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetDesignerSource(string designerPath)
        {
            EditorTab designerTab;
            if (_tabsByPath.TryGetValue(designerPath, out designerTab))
            {
                return designerTab.Doc.Text;
            }
            return File.Exists(designerPath) ? File.ReadAllText(designerPath) : string.Empty;
        }

        private void WriteDesignerSource(string designerPath, string text)
        {
            EditorTab designerTab;
            if (_tabsByPath.TryGetValue(designerPath, out designerTab))
            {
                if (designerTab.Editor.GetText() != text)
                {
                    designerTab.Editor.SetText(text);
                }
                return;
            }
            if (!File.Exists(designerPath) || File.ReadAllText(designerPath) != text)
            {
                File.WriteAllText(designerPath, text);
                WorkspaceManager.Instance.AddOrUpdateDocument(designerPath, text);
            }
        }

        private void InsertEventHandlerMethod(string mainPath, string designerPath, string handlerName, string className)
        {
            EditorTab mainTab;
            if (_tabsByPath.TryGetValue(mainPath, out mainTab) && mainPath != designerPath)
            {
                var updated = InitializeComponentWriter.EnsureEventHandlerMethod(mainTab.Doc.Text, handlerName, className);
                if (updated != mainTab.Doc.Text)
                {
                    mainTab.Editor.SetText(updated);
                }
                return;
            }
            var designerText = GetDesignerSource(designerPath);
            var updatedDesigner = InitializeComponentWriter.EnsureEventHandlerMethod(designerText, handlerName);
            if (updatedDesigner != designerText)
            {
                WriteDesignerSource(designerPath, updatedDesigner);
            }
        }

        private EditorTab ActiveTab
        {
            get { return _tabs.FirstOrDefault(t => ReferenceEquals(t.Page, editorTabs.SelectedTab)); }
        }

        private ProjectBase ActiveProject
        {
            get { return solutionExplorer.StartupProject ?? _projects.FirstOrDefault(); }
        }

        private string Configuration
        {
            get { return configCombo.SelectedItem as string ?? "Debug"; }
        }

        private void UpdateCaretStatus(object sender = null, EventArgs e = null)
        {
            var tab = ActiveTab;
            if (tab == null)
            {
                statusLabel.Text = "Ready";
                return;
            }
            int line, column;
            tab.Editor.GetCaretLocation(out line, out column);
            statusLabel.Text = string.Format("Line {0}, Column {1}", line, column);
        }

        private void SafeOutput(string line)
        {
            if (IsDisposed)
            {
                return;
            }
            try
            {
                BeginInvoke(new Action(() => outputPanel.AppendLine(line)));
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async void BuildClick(object sender, EventArgs e)
        {
            await BuildInternal(false);
        }

        private async void RebuildClick(object sender, EventArgs e)
        {
            await BuildInternal(true);
        }

        private async Task BuildInternal(bool rebuild)
        {
            var project = ActiveProject;
            if (project == null)
            {
                outputPanel.AppendLine("No project is loaded. Use File > New Project or File > Open Project.");
                SelectBottomTab("Output");
                return;
            }
            if (_building)
            {
                return;
            }
            _building = true;
            UpdateDebugUi();
            buildStatusLabel.Text = "Building...";
            try
            {
                SaveAllFiles();
                outputPanel.AppendLine(string.Format("------ Build started: {0} ({1}, {2}) ------", project.Name, Configuration, project.Platform));
                var result = project.Language == DevLanguage.CSharp
                    ? await _csharpBuilder.BuildAsync(project, Configuration)
                    : await _cppBuilder.BuildAsync(project, Configuration);
                errorListPanel.SetDiagnostics(result.Diagnostics);
                var errors = result.Diagnostics.Count(d => d.Severity == DiagnosticSeverityEx.Error);
                var warnings = result.Diagnostics.Count(d => d.Severity == DiagnosticSeverityEx.Warning);
                outputPanel.AppendLine(string.Format("========== Build {0}: {1} error(s), {2} warning(s) in {3:0.0}s ==========",
                    result.Success ? "succeeded" : "failed", errors, warnings, result.Duration.TotalSeconds));
                if (result.Success)
                {
                    outputPanel.AppendLine("Output: " + result.OutputPath);
                }
                buildStatusLabel.Text = result.Success ? "Build succeeded" : "Build failed";
                if (result.Diagnostics.Count > 0)
                {
                    SelectBottomTab("ErrorList");
                }
                else
                {
                    SelectBottomTab("Output");
                }
            }
            catch (Exception ex)
            {
                outputPanel.AppendLine("Build error: " + ex.Message);
                buildStatusLabel.Text = "Build failed";
            }
            finally
            {
                _building = false;
                UpdateDebugUi();
            }
        }

        private void SelectBottomTab(string name)
        {
            foreach (TabPage page in bottomTabs.TabPages)
            {
                if (string.Equals(page.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    bottomTabs.SelectedTab = page;
                    return;
                }
            }
        }

        private async void StartDebugClick(object sender, EventArgs e)
        {
            if (_debugger.IsRunning && _debugger.IsStopped)
            {
                _debugger.Continue();
                return;
            }
            if (_debugger.IsRunning || _building)
            {
                return;
            }
            var project = ActiveProject;
            if (project == null)
            {
                outputPanel.AppendLine("No project is loaded.");
                return;
            }
            await BuildInternal(false);
            if (_building || buildStatusLabel.Text == "Build failed")
            {
                return;
            }
            var exe = project.FullOutputPath;
            if (!File.Exists(exe))
            {
                outputPanel.AppendLine("Executable not found: " + exe);
                return;
            }
            if (project.Language == DevLanguage.Cpp)
            {
                outputPanel.AppendLine("Debugging is only supported for C# programs. Running without debugging...");
                _cppBuilder.Run(exe, outputPanel.AppendLine);
                return;
            }
            try
            {
                _debugger.Start(exe, CollectBreakpoints());
                outputPanel.AppendLine("Debugger attached to " + exe);
            }
            catch (Exception ex)
            {
                outputPanel.AppendLine("Debugger error: " + ex.Message);
            }
            UpdateDebugUi();
        }

        private async void StartWithoutDebugClick(object sender, EventArgs e)
        {
            if (_debugger.IsRunning || _building)
            {
                return;
            }
            var project = ActiveProject;
            if (project == null)
            {
                outputPanel.AppendLine("No project is loaded.");
                return;
            }
            await BuildInternal(false);
            if (_building || buildStatusLabel.Text == "Build failed")
            {
                return;
            }
            var exe = project.FullOutputPath;
            if (!File.Exists(exe))
            {
                outputPanel.AppendLine("Executable not found: " + exe);
                return;
            }
            if (project.Language == DevLanguage.Cpp)
            {
                _cppBuilder.Run(exe, outputPanel.AppendLine);
            }
            else
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = project.OutputPath.Length > 0 ? Path.GetDirectoryName(exe) : Environment.CurrentDirectory,
                    UseShellExecute = false
                });
            }
        }

        private List<BreakpointSpec> CollectBreakpoints()
        {
            var specs = new List<BreakpointSpec>();
            foreach (var tab in _tabs)
            {
                foreach (var line in tab.Doc.BreakpointLines)
                {
                    specs.Add(new BreakpointSpec { File = tab.Doc.FilePath, Line = line });
                }
            }
            return specs;
        }

        private void StopDebugClick(object sender, EventArgs e)
        {
            if (_debugger.IsRunning)
            {
                _debugger.Stop();
            }
        }

        private string ResolveDebugValue(string name)
        {
            var info = _lastStopInfo;
            if (info == null || !_debugger.IsStopped || string.IsNullOrEmpty(name))
            {
                return null;
            }
            foreach (var variable in info.Locals)
            {
                if (string.Equals(variable.Name, name, StringComparison.Ordinal))
                {
                    return string.IsNullOrEmpty(variable.TypeName)
                        ? variable.Value
                        : variable.Value + "  (" + variable.TypeName + ")";
                }
            }
            return null;
        }

        private void StepOverClick(object sender, EventArgs e)
        {
            if (_debugger.IsStopped)
            {
                _debugger.Step(false);
            }
        }

        private void StepIntoClick(object sender, EventArgs e)
        {
            if (_debugger.IsStopped)
            {
                _debugger.Step(true);
            }
        }

        private void StepOutClick(object sender, EventArgs e)
        {
            if (_debugger.IsStopped)
            {
                _debugger.StepOut();
            }
        }

        private void OnDebuggerStopped(DebugStopInfo info)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<DebugStopInfo>(OnDebuggerStopped), info);
                return;
            }
            _lastStopInfo = info;
            callStackPanel.SetCallStack(info.CallStack);
            localsPanel.SetLocals(info.Locals);
            var reason = info.Reason ?? "Break";
            if (!string.IsNullOrEmpty(info.ExceptionMessage))
            {
                reason = reason + ": " + info.ExceptionMessage;
            }
            statusLabel.Text = reason;
            outputPanel.AppendLine("Debugger stopped: " + reason);
            if (!info.IsStep)
            {
                SelectBottomTab("CallStack");
            }
            var frame = info.CallStack.FirstOrDefault(f => !string.IsNullOrEmpty(f.File));
            if (!info.IsStep && info.ExceptionMessage == null && frame != null)
            {
                statusLabel.Text = "Breakpoint hit: " + Path.GetFileName(frame.File) + ":" + frame.Line;
            }
            if (frame != null)
            {
                NavigateTo(frame.File, frame.Line, 1);
                EditorTab tab;
                if (_tabsByPath.TryGetValue(Path.GetFullPath(frame.File), out tab))
                {
                    editorTabs.SelectedTab = tab.Page;
                    if (tab.InnerTabs != null)
                    {
                        tab.InnerTabs.SelectedTab = tab.CodePage;
                    }
                    tab.Editor.SetDebugLine(frame.Line);
                    tab.Editor.Focus();
                }
                if (WindowState == FormWindowState.Minimized)
                {
                    WindowState = FormWindowState.Normal;
                }
                Activate();
            }
            UpdateDebugUi();
        }

        private void OnDebuggerResumed()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(OnDebuggerResumed));
                return;
            }
            _lastStopInfo = null;
            ClearDebugLines();
            statusLabel.Text = "Running";
            UpdateDebugUi();
        }

        private void OnDebuggerExited()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(OnDebuggerExited));
                return;
            }
            _lastStopInfo = null;
            ClearDebugLines();
            callStackPanel.Clear();
            localsPanel.Clear();
            outputPanel.AppendLine("Program exited.");
            statusLabel.Text = "Ready";
            UpdateDebugUi();
        }

        private void ClearDebugLines()
        {
            foreach (var tab in _tabs)
            {
                tab.Editor.SetDebugLine(-1);
            }
        }

        private void UpdateDebugUi()
        {
            bool running = _debugger.IsRunning;
            bool stopped = _debugger.IsStopped;
            bool canBuild = !_building && !running;
            stopButton.Enabled = stopDebugMenu.Enabled = running;
            stepOverButton.Enabled = stepOverMenu.Enabled = stopped;
            stepIntoButton.Enabled = stepIntoMenu.Enabled = stopped;
            stepOutButton.Enabled = stepOutMenu.Enabled = stopped;
            buildButton.Enabled = buildSolutionMenu.Enabled = rebuildSolutionMenu.Enabled = canBuild;
            startButton.Text = stopped ? "Continue" : "Start";
            startDebugMenu.Text = stopped ? "Continue" : "Start Debugging";
        }

        private async Task GoToDefinition()
        {
            var tab = ActiveTab;
            if (tab == null || tab.Doc.Language != DevLanguage.CSharp)
            {
                return;
            }
            var location = await _navigation.GoToDefinitionAsync(tab.Doc.FilePath, tab.Editor.CaretOffset);
            if (location == null)
            {
                return;
            }
            if (!string.IsNullOrEmpty(location.FilePath) && location.Line > 0)
            {
                NavigateTo(location.FilePath, location.Line, location.Column);
            }
            else
            {
                outputPanel.AppendLine(location.Description);
                SelectBottomTab("Output");
            }
        }

        private async Task FindReferences()
        {
            var tab = ActiveTab;
            if (tab == null || tab.Doc.Language != DevLanguage.CSharp)
            {
                return;
            }
            var symbolName = GetWordAtCaret(tab);
            var locations = await _navigation.FindReferencesAsync(tab.Doc.FilePath, tab.Editor.CaretOffset);
            var hits = locations.Select(l => new FindHit
            {
                File = l.FilePath,
                Line = l.Line,
                Column = l.Column,
                LineText = l.Description
            }).ToList();
            findResultsPanel.SetResults(string.IsNullOrEmpty(symbolName) ? "references" : symbolName, hits);
            statusLabel.Text = hits.Count + " reference(s) found";
            SelectBottomTab("FindResults");
        }

        private string GetWordAtCaret(EditorTab tab)
        {
            int line, column;
            tab.Editor.GetCaretLocation(out line, out column);
            var text = tab.Doc.Text;
            int lineStart = tab.Doc.GetLineOffset(line);
            int lineLength = tab.Doc.GetLineLength(line);
            int index = Math.Min(column - 1, lineLength);
            int start = index;
            while (start > 0 && (char.IsLetterOrDigit(text[lineStart + start - 1]) || text[lineStart + start - 1] == '_'))
            {
                start--;
            }
            int end = index;
            while (end < lineLength && (char.IsLetterOrDigit(text[lineStart + end]) || text[lineStart + end] == '_'))
            {
                end++;
            }
            return end > start ? text.Substring(lineStart + start, end - start) : null;
        }

        private void NavigateTo(string file, int line, int column)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                return;
            }
            OpenFile(file);
            EditorTab tab;
            if (!_tabsByPath.TryGetValue(Path.GetFullPath(file), out tab))
            {
                return;
            }
            if (line > 0)
            {
                tab.Editor.GotoLine(line);
                if (column > 1)
                {
                    try
                    {
                        var offset = tab.Doc.GetLineOffset(line) + Math.Min(column - 1, tab.Doc.GetLineLength(line));
                        tab.Editor.CaretOffset = offset;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                    }
                }
            }
        }

        private void FindClick(object sender, EventArgs e)
        {
            using (var dialog = new FindDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                var hits = FindService.Search(GetSearchFiles(), dialog.Query, dialog.MatchCase, dialog.WholeWord, dialog.UseRegex);
                findResultsPanel.SetResults(dialog.Query, hits);
                statusLabel.Text = hits.Count + " match(es) found";
                SelectBottomTab("FindResults");
            }
        }

        private IEnumerable<string> GetSearchFiles()
        {
            var files = new List<string>();
            foreach (var project in _projects)
            {
                foreach (var item in project.Items)
                {
                    if (File.Exists(item.FullPath))
                    {
                        files.Add(item.FullPath);
                    }
                }
            }
            foreach (var tab in _tabs)
            {
                if (!files.Contains(tab.Doc.FilePath, StringComparer.OrdinalIgnoreCase))
                {
                    files.Add(tab.Doc.FilePath);
                }
            }
            return files.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private void GoToLineClick(object sender, EventArgs e)
        {
            var tab = ActiveTab;
            if (tab == null)
            {
                return;
            }
            var text = Microsoft.VisualBasic.Interaction.InputBox("Line number (1-" + tab.Doc.LineCount + "):", "Go To Line", "1");
            int line;
            if (int.TryParse(text, out line) && line >= 1)
            {
                tab.Editor.GotoLine(Math.Min(line, tab.Doc.LineCount));
            }
        }

        private void ToggleBreakpointClick(object sender, EventArgs e)
        {
            var tab = ActiveTab;
            if (tab == null)
            {
                return;
            }
            int line = tab.Editor.CaretLine;
            if (!tab.Doc.BreakpointLines.Remove(line))
            {
                tab.Doc.BreakpointLines.Add(line);
            }
            tab.Editor.RefreshBreakpoints();
            RefreshBreakpointsPanel();
        }

        private void RefreshBreakpointsPanel()
        {
            var entries = new List<BreakpointsPanel.BreakpointEntry>();
            foreach (var tab in _tabs)
            {
                foreach (var line in tab.Doc.BreakpointLines)
                {
                    entries.Add(new BreakpointsPanel.BreakpointEntry { File = tab.Doc.FilePath, Line = line });
                }
            }
            breakpointsPanel.SetBreakpoints(entries);
        }

        private void RemoveBreakpoint(string file, int line)
        {
            EditorTab tab;
            if (_tabsByPath.TryGetValue(file, out tab))
            {
                tab.Doc.BreakpointLines.Remove(line);
                tab.Editor.RefreshBreakpoints();
                RefreshBreakpointsPanel();
            }
        }

        private void SaveClick(object sender, EventArgs e)
        {
            var tab = ActiveTab;
            if (tab == null)
            {
                return;
            }
            File.WriteAllText(tab.Doc.FilePath, tab.Doc.Text);
            tab.Doc.Dirty = false;
            tab.Page.Text = tab.Doc.DisplayName;
            statusLabel.Text = "Saved " + tab.Doc.FilePath;
        }

        private void SaveAllClick(object sender, EventArgs e)
        {
            SaveAllFiles();
            statusLabel.Text = "All files saved";
        }

        private void SaveAllFiles()
        {
            foreach (var tab in _tabs)
            {
                if (tab.Doc.Dirty)
                {
                    File.WriteAllText(tab.Doc.FilePath, tab.Doc.Text);
                    tab.Doc.Dirty = false;
                    tab.Page.Text = tab.Doc.DisplayName;
                }
            }
            foreach (var project in _projects)
            {
                project.Save();
            }
        }

        private void NewProjectClick(object sender, EventArgs e)
        {
            using (var dialog = new NewProjectDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                string proxyTarget = null;
                List<string> proxyExports = null;
                if (dialog.Language == DevLanguage.Cpp && dialog.OutputKind == OutputKindEx.Library && dialog.IsProxyDll)
                {
                    using (var fileDialog = new OpenFileDialog())
                    {
                        fileDialog.Title = "Select the original DLL to forward exports to";
                        fileDialog.Filter = "Dynamic-Link Library (*.dll)|*.dll|All Files (*.*)|*.*";
                        if (fileDialog.ShowDialog(this) != DialogResult.OK)
                        {
                            return;
                        }
                        try
                        {
                            proxyExports = PeExportReader.GetExportNames(fileDialog.FileName);
                        }
                        catch (Exception ex)
                        {
                            proxyExports = new List<string>();
                            outputPanel.AppendLine("Warning: could not read exports from " + fileDialog.FileName + ": " + ex.Message);
                        }
                        proxyTarget = Path.GetFileNameWithoutExtension(fileDialog.FileName);
                        outputPanel.AppendLine("Found " + proxyExports.Count + " exported functions in " + Path.GetFileName(fileDialog.FileName));
                    }
                }
                CloseAllProjects();
                Directory.CreateDirectory(dialog.ProjectDirectory);
                ProjectBase project;
                if (dialog.Language == DevLanguage.CSharp)
                {
                    var csprojPath = Path.Combine(dialog.ProjectDirectory, dialog.ProjectName + ".csproj");
                    project = CSharpProject.CreateNew(csprojPath, dialog.ProjectName, dialog.OutputKind);
                    if (dialog.OutputKind == OutputKindEx.WindowsApplication)
                    {
                        var formPath = Path.Combine(dialog.ProjectDirectory, "MainForm.cs");
                        var designerPath = Path.Combine(dialog.ProjectDirectory, "MainForm.Designer.cs");
                        var programPath = Path.Combine(dialog.ProjectDirectory, "Program.cs");
                        if (!File.Exists(formPath))
                        {
                            File.WriteAllText(formPath, NewCSharpFormTemplate(dialog.ProjectName));
                        }
                        if (!File.Exists(designerPath))
                        {
                            File.WriteAllText(designerPath, NewCSharpFormDesignerTemplate(dialog.ProjectName));
                        }
                        if (!File.Exists(programPath))
                        {
                            File.WriteAllText(programPath, NewCSharpWinAppTemplate(dialog.ProjectName));
                        }
                        ((CSharpProject)project).AddFile(formPath);
                        ((CSharpProject)project).AddFile(designerPath);
                        ((CSharpProject)project).AddFile(programPath);
                    }
                    else if (dialog.OutputKind == OutputKindEx.Library)
                    {
                        var classPath = Path.Combine(dialog.ProjectDirectory, "Class1.cs");
                        if (!File.Exists(classPath))
                        {
                            File.WriteAllText(classPath, NewCSharpClassTemplate(dialog.ProjectName));
                        }
                        ((CSharpProject)project).AddFile(classPath);
                    }
                    else
                    {
                        var programPath = Path.Combine(dialog.ProjectDirectory, "Program.cs");
                        if (!File.Exists(programPath))
                        {
                            File.WriteAllText(programPath, NewCSharpTemplate(dialog.ProjectName));
                        }
                        ((CSharpProject)project).AddFile(programPath);
                    }
                    project.Save();
                    WorkspaceManager.Instance.LoadProject((CSharpProject)project);
                }
                else
                {
                    var vcxprojPath = Path.Combine(dialog.ProjectDirectory, dialog.ProjectName + ".vcxproj");
                    project = CppProjectFactory.CreateNew(vcxprojPath, dialog.ProjectName, dialog.OutputKind);
                    var mainPath = Path.Combine(dialog.ProjectDirectory, "main.cpp");
                    if (!File.Exists(mainPath))
                    {
                        if (dialog.OutputKind == OutputKindEx.Library && dialog.IsProxyDll && proxyTarget != null && proxyExports != null)
                        {
                            File.WriteAllText(mainPath, NewCppProxyTemplate(proxyTarget, proxyExports));
                        }
                        else
                        {
                            File.WriteAllText(mainPath, dialog.OutputKind == OutputKindEx.Library
                                ? NewCppLibraryTemplate(dialog.ProjectName, false)
                                : NewCppTemplate());
                        }
                    }
                    ((CppProject)project).AddFile(mainPath);
                }
                AddProjectToSolution(project);
                outputPanel.AppendLine("Created project " + project.Name + " in " + project.ProjectDirectory);
            }
        }

        private void NewFileClick(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "C# Source (*.cs)|*.cs|C++ Source (*.cpp)|*.cpp|C Source (*.c)|*.c|Header (*.h)|*.h|All Files (*.*)|*.*";
                dialog.Title = "New File";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                if (!File.Exists(dialog.FileName))
                {
                    File.WriteAllText(dialog.FileName, string.Empty);
                }
                var project = ActiveProject;
                if (project != null && MatchesProjectLanguage(project, dialog.FileName)
                    && !project.Items.Any(i => string.Equals(i.FullPath, dialog.FileName, StringComparison.OrdinalIgnoreCase)))
                {
                    AddFileToProject(project, dialog.FileName);
                }
                OpenFile(dialog.FileName);
            }
        }

        private static void AddFileToProject(ProjectBase project, string fullPath)
        {
            if (project is CSharpProject)
            {
                ((CSharpProject)project).AddFile(fullPath);
            }
            else if (project is CppProject)
            {
                ((CppProject)project).AddFile(fullPath);
            }
            else
            {
                return;
            }
            project.Save();
        }

        private static bool MatchesProjectLanguage(ProjectBase project, string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (project.Language == DevLanguage.Cpp)
            {
                return ext == ".cpp" || ext == ".cc" || ext == ".cxx" || ext == ".c" || ext == ".h" || ext == ".hpp";
            }
            return ext == ".cs" || ext == ".resx";
        }

        private static bool IsSourceForCpp(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext == ".cpp" || ext == ".c" || ext == ".h" || ext == ".hpp";
        }

        private void OpenProjectClick(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Project Files (*.csproj;*.vcxproj)|*.csproj;*.vcxproj|C# Project (*.csproj)|*.csproj|C++ Project (*.vcxproj)|*.vcxproj";
                dialog.Title = "Open Project";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                OpenProject(dialog.FileName);
            }
        }

        private void OpenProject(string path)
        {
            CloseAllProjects();
            ProjectBase project;
            var extension = Path.GetExtension(path).ToLowerInvariant();
            try
            {
                if (extension == ".csproj")
                {
                    var csProject = new CSharpProject(path);
                    WorkspaceManager.Instance.LoadProject(csProject);
                    project = csProject;
                }
                else if (extension == ".vcxproj")
                {
                    project = new CppProject(path);
                }
                else
                {
                    outputPanel.AppendLine("Unsupported project type: " + path);
                    return;
                }
            }
            catch (Exception ex)
            {
                outputPanel.AppendLine("Failed to load project: " + ex.Message);
                return;
            }
            AddProjectToSolution(project);
        }

        private void AddProjectToSolution(ProjectBase project)
        {
            if (_projects.Any(p => string.Equals(p.FilePath, project.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            _projects.Add(project);
            solutionExplorer.LoadProject(project);
            statusLabel.Text = "Loaded project " + project.Name;
            Text = "DevStudio - " + project.Name;
        }

        private void OpenSingleFileClick(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Source Files (*.cs;*.cpp;*.cc;*.cxx;*.c;*.h;*.hpp)|*.cs;*.cpp;*.cc;*.cxx;*.c;*.h;*.hpp|All Files (*.*)|*.*";
                dialog.Title = "Open Single File";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                OpenSingleFile(dialog.FileName);
            }
        }

        private void OpenSingleFile(string path)
        {
            CloseAllProjects();
            OpenFile(path);
            if (_projects.Any(p => p is SingleFileProject && string.Equals(p.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            var project = new SingleFileProject(path);
            _projects.Add(project);
            solutionExplorer.LoadProject(project);
            if (Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                WorkspaceManager.Instance.SetSingleFile(path);
            }
        }

        private void AddExistingFile(ProjectBase project)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = project.Language == DevLanguage.Cpp
                    ? "C/C++ Files (*.cpp;*.cc;*.cxx;*.c;*.h;*.hpp)|*.cpp;*.cc;*.cxx;*.c;*.h;*.hpp|All Files (*.*)|*.*"
                    : "C# Files (*.cs)|*.cs|All Files (*.*)|*.*";
                dialog.Title = "Add Existing File";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                AddFileToProject(project, dialog.FileName);
                OpenFile(dialog.FileName);
            }
        }

        private void ShowAddReference(ProjectBase project)
        {
            if (project.Language != DevLanguage.CSharp)
            {
                MessageBox.Show(this, "DLL references are only supported for C# projects.", "DevStudio",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dialog = new AddReferenceDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedReference == null)
                {
                    return;
                }
                var csProject = project as CSharpProject;
                if (csProject == null)
                {
                    return;
                }
                csProject.AddReference(dialog.SelectedReference);
                csProject.Save();
                solutionExplorer.RefreshProject(csProject);
                WorkspaceManager.Instance.LoadProject(csProject);
                outputPanel.AppendLine("Added reference: " + dialog.SelectedReference);
            }
        }

        private void AddReferenceClick(object sender, EventArgs e)
        {
            var project = ActiveProject;
            if (project == null)
            {
                MessageBox.Show(this, "No project is loaded.", "DevStudio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ShowAddReference(project);
        }

        private void RemoveFileFromProject(ProjectItem item)
        {
            var project = _projects.FirstOrDefault(p => p.Items.Contains(item));
            if (project == null)
            {
                return;
            }
            project.Items.Remove(item);
            project.Save();
            if (project is CSharpProject)
            {
                WorkspaceManager.Instance.RemoveDocument(item.FullPath);
            }
            solutionExplorer.RefreshProject(project);
            EditorTab tab;
            if (_tabsByPath.TryGetValue(item.FullPath, out tab))
            {
                CloseTab(tab);
            }
        }

        private void CloseProject(ProjectBase project)
        {
            var paths = new HashSet<string>(project.Items.Select(i => i.FullPath), StringComparer.OrdinalIgnoreCase);
            foreach (var tab in _tabs.Where(t => paths.Contains(t.Doc.FilePath)).ToList())
            {
                CloseTab(tab);
            }
            _projects.Remove(project);
            if (project is CSharpProject)
            {
                foreach (var item in project.Items)
                {
                    WorkspaceManager.Instance.RemoveDocument(item.FullPath);
                }
            }
            solutionExplorer.RemoveProject(project);
            var remainingCSharp = _projects.OfType<CSharpProject>().FirstOrDefault();
            if (remainingCSharp != null)
            {
                WorkspaceManager.Instance.LoadProject(remainingCSharp);
                foreach (var tab in _tabs.Where(t => t.Doc.Language == DevLanguage.CSharp).ToList())
                {
                    WorkspaceManager.Instance.AddOrUpdateDocument(tab.Doc.FilePath, tab.Doc.Text);
                }
            }
            else
            {
                WorkspaceManager.Instance.Reset();
            }
            ClearDebugLines();
            callStackPanel.Clear();
            localsPanel.Clear();
            if (ActiveProject != null)
            {
                Text = "DevStudio - " + ActiveProject.Name;
            }
            else
            {
                Text = "DevStudio";
            }
            UpdateDebugUi();
            statusLabel.Text = "Closed project " + project.Name;
        }

        private void CloseAllProjects()
        {
            foreach (var tab in _tabs.ToList())
            {
                CloseTab(tab);
            }
            foreach (var project in _projects.ToList())
            {
                if (project is CSharpProject)
                {
                    foreach (var item in project.Items)
                    {
                        WorkspaceManager.Instance.RemoveDocument(item.FullPath);
                    }
                }
            }
            _projects.Clear();
            solutionExplorer.ClearAll();
            WorkspaceManager.Instance.Reset();
            ClearDebugLines();
            outputPanel.Clear();
            errorListPanel.Clear();
            findResultsPanel.Clear();
            callStackPanel.Clear();
            breakpointsPanel.Clear();
            localsPanel.Clear();
            Text = "DevStudio";
            UpdateDebugUi();
        }

        private void AddNewFile(ProjectBase project)
        {
            using (var dialog = new AddNewFileDialog(project.Language, project.Name))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    return;
                }
                var fullPath = Path.Combine(project.ProjectDirectory, dialog.FileName);
                if (File.Exists(fullPath))
                {
                    MessageBox.Show(this, "A file with this name already exists in the project directory.", "DevStudio",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                File.WriteAllText(fullPath, dialog.GetTemplateContent());
                AddFileToProject(project, fullPath);
                if (dialog.HasDesignerFile)
                {
                    var designerPath = Path.Combine(project.ProjectDirectory,
                        Path.GetFileNameWithoutExtension(dialog.FileName) + ".Designer.cs");
                    if (!File.Exists(designerPath))
                    {
                        File.WriteAllText(designerPath, dialog.GetDesignerFileContent());
                        AddFileToProject(project, designerPath);
                    }
                }
                solutionExplorer.RefreshProject(project);
                OpenFile(fullPath);
                statusLabel.Text = "Added " + dialog.FileName;
            }
        }

        private void CloseTab(EditorTab tab)
        {
            _tabs.Remove(tab);
            _tabsByPath.Remove(tab.Doc.FilePath);
            editorTabs.TabPages.Remove(tab.Page);
            tab.Page.Controls.Clear();
            tab.Editor.Dispose();
            if (tab.Designer != null)
            {
                tab.Designer.Dispose();
            }
        }

        private void ToggleMinimapClick(object sender, EventArgs e)
        {
            _minimapVisible = !_minimapVisible;
            foreach (var tab in _tabs)
            {
                tab.Editor.ShowMinimap(_minimapVisible);
            }
        }

        private void PlatformChanged(object sender, EventArgs e)
        {
            var project = ActiveProject;
            if (project == null)
            {
                return;
            }
            var platform = platformCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(platform) || string.Equals(project.Platform, platform, StringComparison.Ordinal))
            {
                return;
            }
            project.Platform = platform;
            if (project is CSharpProject)
            {
                WorkspaceManager.Instance.UpdateCompilationOptions();
            }
            statusLabel.Text = "Platform set to " + platform;
        }

        private void ExitClick(object sender, EventArgs e)
        {
            Close();
        }

        private void AboutClick(object sender, EventArgs e)
        {
            using (var dialog = new AboutDialog())
            {
                dialog.ShowDialog(this);
            }
        }

        private void SetupSnippetMenu()
        {
            var snippetMenu = new ToolStripMenuItem("Insert Snippet");

            var cmdMenu = new ToolStripMenuItem("Start cmd.exe");
            cmdMenu.DropDownItems.Add(new ToolStripMenuItem("C#", null, (s, e) => InsertSnippet(CSharpCmdSnippet)));
            cmdMenu.DropDownItems.Add(new ToolStripMenuItem("C++", null, (s, e) => InsertSnippet(CppCmdSnippet)));

            var shellMenu = new ToolStripMenuItem("Start Shell");
            shellMenu.DropDownItems.Add(new ToolStripMenuItem("C#", null, (s, e) => InsertSnippet(CSharpShellSnippet)));
            shellMenu.DropDownItems.Add(new ToolStripMenuItem("C++", null, (s, e) => InsertSnippet(CppShellSnippet)));

            var elevateMenu = new ToolStripMenuItem("Run as Administrator");
            elevateMenu.DropDownItems.Add(new ToolStripMenuItem("C#", null, (s, e) => InsertSnippet(CSharpElevateSnippet)));
            elevateMenu.DropDownItems.Add(new ToolStripMenuItem("C++", null, (s, e) => InsertSnippet(CppElevateSnippet)));

            snippetMenu.DropDownItems.Add(cmdMenu);
            snippetMenu.DropDownItems.Add(shellMenu);
            snippetMenu.DropDownItems.Add(elevateMenu);
            editMenu.DropDownItems.Insert(0, snippetMenu);
            editMenu.DropDownItems.Insert(1, new ToolStripSeparator());
        }

        private void InsertSnippet(string snippet)
        {
            var tab = ActiveTab;
            if (tab == null)
            {
                return;
            }
            tab.Editor.InsertAtCaret(snippet);
        }

        private const string CSharpCmdSnippet =
            "System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo\r\n" +
            "{\r\n" +
            "    FileName = \"cmd.exe\",\r\n" +
            "    UseShellExecute = true\r\n" +
            "});";

        private const string CppCmdSnippet =
            "ShellExecute(NULL, TEXT(\"open\"), TEXT(\"cmd.exe\"), NULL, NULL, SW_SHOW);";

        private const string CSharpShellSnippet =
            "System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo\r\n" +
            "{\r\n" +
            "    FileName = \"powershell.exe\",\r\n" +
            "    UseShellExecute = true\r\n" +
            "});";

        private const string CppShellSnippet =
            "ShellExecute(NULL, TEXT(\"open\"), TEXT(\"powershell.exe\"), NULL, NULL, SW_SHOW);";

        private const string CSharpElevateSnippet =
            "var startInfo = new System.Diagnostics.ProcessStartInfo\r\n" +
            "{\r\n" +
            "    FileName = \"cmd.exe\",\r\n" +
            "    Arguments = \"/k\",\r\n" +
            "    UseShellExecute = true,\r\n" +
            "    Verb = \"runas\"\r\n" +
            "};\r\n" +
            "System.Diagnostics.Process.Start(startInfo);";

        private const string CppElevateSnippet =
            "SHELLEXECUTEINFO executeInfo = { 0 };\r\n" +
            "executeInfo.cbSize = sizeof(SHELLEXECUTEINFO);\r\n" +
            "executeInfo.lpVerb = TEXT(\"runas\");\r\n" +
            "executeInfo.lpFile = TEXT(\"cmd.exe\");\r\n" +
            "executeInfo.nShow = SW_SHOW;\r\n" +
            "ShellExecuteEx(&executeInfo);";

        private static string NewCppProxyTemplate(string moduleName, List<string> exports)
        {
            var nl = Environment.NewLine;
            var sb = new StringBuilder();
            sb.Append("// Proxy DLL - forwards all exported functions to " + moduleName + ".dll." + nl);
            sb.Append("// Build with the MSVC linker for /export forwarding; MinGW ignores #pragma comment." + nl);
            sb.Append("#include <windows.h>" + nl);
            sb.Append(nl);
            var identifierRegex = new Regex("^[A-Za-z_][A-Za-z0-9_]*$");
            foreach (var export in exports)
            {
                if (!identifierRegex.IsMatch(export))
                {
                    continue;
                }
                sb.Append("#pragma comment(linker, \"/export:" + export + "=" + moduleName + "." + export + "\")" + nl);
            }
            sb.Append(nl);
            sb.Append("BOOL WINAPI DllMain(HINSTANCE hinst, DWORD reason, LPVOID reserved)" + nl);
            sb.Append("{" + nl);
            sb.Append("    switch (reason)" + nl);
            sb.Append("    {" + nl);
            sb.Append("        case DLL_PROCESS_ATTACH:" + nl);
            sb.Append("        case DLL_PROCESS_DETACH:" + nl);
            sb.Append("            break;" + nl);
            sb.Append("    }" + nl);
            sb.Append("    return TRUE;" + nl);
            sb.Append("}" + nl);
            return sb.ToString();
        }

        private void MainFormFormClosing(object sender, FormClosingEventArgs e)
        {
            var dirty = _tabs.Where(t => t.Doc.Dirty).Select(t => t.Doc.DisplayName).ToList();
            if (dirty.Count > 0)
            {
                var choice = MessageBox.Show(this,
                    "Save changes to " + string.Join(", ", dirty) + "?",
                    "DevStudio", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (choice == DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (choice == DialogResult.Yes)
                {
                    SaveAllFiles();
                }
            }
            if (_debugger.IsRunning)
            {
                _debugger.Stop();
            }
            _debugger.Dispose();
        }

        private static string NewCSharpTemplate(string projectName)
        {
            return "using System;" + Environment.NewLine +
                   Environment.NewLine +
                   "namespace " + projectName + Environment.NewLine +
                   "{" + Environment.NewLine +
                   "    public static class Program" + Environment.NewLine +
                   "    {" + Environment.NewLine +
                   "        public static void Main(string[] args)" + Environment.NewLine +
                   "        {" + Environment.NewLine +
                   "            Console.WriteLine(\"Hello from " + projectName + "!\");" + Environment.NewLine +
                   "            Console.ReadLine();" + Environment.NewLine +
                   "        }" + Environment.NewLine +
                   "    }" + Environment.NewLine +
                   "}" + Environment.NewLine;
        }

        private static string NewCppTemplate()
        {
            return "#include <iostream>" + Environment.NewLine +
                   Environment.NewLine +
                   "int main()" + Environment.NewLine +
                   "{" + Environment.NewLine +
                   "    std::cout << \"Hello from DevStudio C++!\" << std::endl;" + Environment.NewLine +
                   "    return 0;" + Environment.NewLine +
                   "}" + Environment.NewLine;
        }

        private static string NewCSharpClassTemplate(string projectName)
        {
            return "using System;" + Environment.NewLine +
                   Environment.NewLine +
                   "namespace " + projectName + Environment.NewLine +
                   "{" + Environment.NewLine +
                   "    public class Class1" + Environment.NewLine +
                   "    {" + Environment.NewLine +
                   "    }" + Environment.NewLine +
                   "}" + Environment.NewLine;
        }

        private static string NewCSharpFormTemplate(string projectName)
        {
            return "using System;" + Environment.NewLine +
                   "using System.Windows.Forms;" + Environment.NewLine +
                   Environment.NewLine +
                   "namespace " + projectName + Environment.NewLine +
                   "{" + Environment.NewLine +
                   "    public partial class MainForm : Form" + Environment.NewLine +
                   "    {" + Environment.NewLine +
                   "        public MainForm()" + Environment.NewLine +
                   "        {" + Environment.NewLine +
                   "            InitializeComponent();" + Environment.NewLine +
                   "        }" + Environment.NewLine +
                   "    }" + Environment.NewLine +
                   "}" + Environment.NewLine;
        }

        private static string NewCSharpFormDesignerTemplate(string projectName)
        {
            return "using System.ComponentModel;" + Environment.NewLine +
                   "using System.Windows.Forms;" + Environment.NewLine +
                   Environment.NewLine +
                   "namespace " + projectName + Environment.NewLine +
                   "{" + Environment.NewLine +
                   "    partial class MainForm" + Environment.NewLine +
                   "    {" + Environment.NewLine +
                   "        private System.ComponentModel.IContainer components = null;" + Environment.NewLine +
                   Environment.NewLine +
                   "        protected override void Dispose(bool disposing)" + Environment.NewLine +
                   "        {" + Environment.NewLine +
                   "            if (disposing && (components != null))" + Environment.NewLine +
                   "            {" + Environment.NewLine +
                   "                components.Dispose();" + Environment.NewLine +
                   "            }" + Environment.NewLine +
                   "            base.Dispose(disposing);" + Environment.NewLine +
                   "        }" + Environment.NewLine +
                   Environment.NewLine +
                   "        #region Windows Form Designer generated code" + Environment.NewLine +
                   Environment.NewLine +
                   "        private void InitializeComponent()" + Environment.NewLine +
                   "        {" + Environment.NewLine +
                   "            this.SuspendLayout();" + Environment.NewLine +
                   "            // " + Environment.NewLine +
                   "            // MainForm" + Environment.NewLine +
                   "            // " + Environment.NewLine +
                   "            this.ClientSize = new System.Drawing.Size(284, 261);" + Environment.NewLine +
                   "            this.Name = \"MainForm\";" + Environment.NewLine +
                   "            this.Text = \"" + projectName + "\";" + Environment.NewLine +
                   "            this.ResumeLayout(false);" + Environment.NewLine +
                   "        }" + Environment.NewLine +
                   Environment.NewLine +
                   "        #endregion" + Environment.NewLine +
                   "    }" + Environment.NewLine +
                   "}" + Environment.NewLine;
        }

        private static string NewCSharpWinAppTemplate(string projectName)
        {
            return "using System;" + Environment.NewLine +
                   "using System.Windows.Forms;" + Environment.NewLine +
                   Environment.NewLine +
                   "namespace " + projectName + Environment.NewLine +
                   "{" + Environment.NewLine +
                   "    public static class Program" + Environment.NewLine +
                   "    {" + Environment.NewLine +
                   "        [STAThread]" + Environment.NewLine +
                   "        public static void Main(string[] args)" + Environment.NewLine +
                   "        {" + Environment.NewLine +
                   "            Application.EnableVisualStyles();" + Environment.NewLine +
                   "            Application.SetCompatibleTextRenderingDefault(false);" + Environment.NewLine +
                   "            Application.Run(new MainForm());" + Environment.NewLine +
                   "        }" + Environment.NewLine +
                   "    }" + Environment.NewLine +
                   "}" + Environment.NewLine;
        }

        private static string NewCppLibraryTemplate(string projectName, bool proxy)
        {
            var nl = Environment.NewLine;
            if (proxy)
            {
                return "// Proxy DLL - exported stub functions. Replace the bodies with" + nl +
                       "// forwarding calls to the original DLL as needed." + nl +
                       "#include <windows.h>" + nl +
                       nl +
                       "extern \"C\" __declspec(dllexport) int __stdcall Proxy_Initialize(HWND hwnd, DWORD reason, LPVOID reserved)" + nl +
                       "{" + nl +
                       "    return 1;" + nl +
                       "}" + nl +
                       nl +
                       "extern \"C\" __declspec(dllexport) void __stdcall Proxy_Shutdown()" + nl +
                       "{" + nl +
                       "}" + nl +
                       nl +
                       "BOOL WINAPI DllMain(HINSTANCE hinst, DWORD reason, LPVOID reserved)" + nl +
                       "{" + nl +
                       "    switch (reason)" + nl +
                       "    {" + nl +
                       "        case DLL_PROCESS_ATTACH:" + nl +
                       "        case DLL_PROCESS_DETACH:" + nl +
                       "            break;" + nl +
                       "    }" + nl +
                       "    return TRUE;" + nl +
                       "}" + nl;
            }
            return "#include <iostream>" + nl +
                   nl +
                   "extern \"C\" __declspec(dllexport) int Add(int a, int b)" + nl +
                   "{" + nl +
                   "    return a + b;" + nl +
                   "}" + nl;
        }
    }
}
