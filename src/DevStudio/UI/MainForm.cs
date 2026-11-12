using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private readonly NavigationService _navigation = new NavigationService();
        private readonly CSharpBuildService _csharpBuilder;
        private readonly CppBuildService _cppBuilder;
        private bool _building;
        private bool _minimapVisible = true;

        public MainForm(string[] args)
        {
            InitializeComponent();
            _csharpBuilder = new CSharpBuildService(outputPanel.AppendLine);
            _cppBuilder = new CppBuildService(outputPanel.AppendLine);
            WireEvents();
            PopulateThemeMenu();
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

        private void WireEvents()
        {
            solutionExplorer.FileOpenRequested += path => OpenFile(path);
            solutionExplorer.AddFileRequested += AddExistingFile;
            solutionExplorer.AddReferenceRequested += ShowAddReference;
            solutionExplorer.FileRemoved += RemoveFileFromProject;
            solutionExplorer.ProjectRemoved += RemoveProject;

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

            var doc = new EditorDocument(path, UiTheme.Current);
            var host = new CodeEditorHost { Dock = DockStyle.Fill };
            host.LoadDocument(doc);
            host.ShowMinimap(_minimapVisible);

            var tab = new EditorTab { Doc = doc, Editor = host };
            host.TextChanged += (s, e) =>
            {
                doc.Dirty = true;
                if (doc.Language == DevLanguage.CSharp)
                {
                    WorkspaceManager.Instance.AddOrUpdateDocument(path, doc.Text.Text);
                }
                UpdateCaretStatus();
            };
            host.CaretMoved += UpdateCaretStatus;
            host.BreakpointToggled += (s, line) =>
            {
                if (!doc.BreakpointLines.Remove(line))
                {
                    doc.BreakpointLines.Add(line);
                }
                RefreshBreakpointsPanel();
            };
            host.GoToDefinitionRequested += (s, e) => { var t = GoToDefinition(); };
            host.FindReferencesRequested += (s, e) => { var t = FindReferences(); };
            host.FindRequested += FindClick;

            var page = new TabPage(doc.DisplayName) { Name = path };
            bool hasDesigner = doc.Language == DevLanguage.CSharp && File.ReadAllText(path).Contains("InitializeComponent");
            if (hasDesigner)
            {
                var designer = new DesignerHostControl { Dock = DockStyle.Fill };
                tab.Designer = designer;
                tab.InnerTabs = new TabControl { Dock = DockStyle.Fill };
                tab.CodePage = new TabPage("Code") { Name = "Code" };
                tab.DesignerPage = new TabPage("Designer") { Name = "Designer" };
                tab.CodePage.Controls.Add(host);
                tab.DesignerPage.Controls.Add(designer);
                tab.InnerTabs.TabPages.Add(tab.CodePage);
                tab.InnerTabs.TabPages.Add(tab.DesignerPage);
                page.Controls.Add(tab.InnerTabs);
                designer.SourceChanged += text =>
                {
                    if (tab.Editor.InnerEditor.Document.Text != text)
                    {
                        tab.Editor.InnerEditor.Document.Text = text;
                    }
                };
                designer.ViewCodeRequested += () => { tab.InnerTabs.SelectedTab = tab.CodePage; };
                tab.InnerTabs.SelectedIndexChanged += (s, e) =>
                {
                    if (tab.InnerTabs.SelectedTab == tab.DesignerPage)
                    {
                        designer.LoadSource(tab.Doc.Text.Text);
                    }
                };
                designer.LoadSource(doc.Text.Text);
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
            var location = tab.Doc.Text.GetLocation(tab.Editor.InnerEditor.CaretOffset);
            statusLabel.Text = string.Format("Line {0}, Column {1}", location.Line, location.Column);
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
            if (_debugger.IsStopped)
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

        private void StartWithoutDebugClick(object sender, EventArgs e)
        {
            var project = ActiveProject;
            if (project == null)
            {
                return;
            }
            var exe = project.FullOutputPath;
            if (!File.Exists(exe))
            {
                outputPanel.AppendLine("Executable not found: " + exe + ". Build the project first.");
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
            callStackPanel.SetCallStack(info.CallStack);
            localsPanel.SetLocals(info.Locals);
            var reason = info.Reason ?? "Break";
            if (!string.IsNullOrEmpty(info.ExceptionMessage))
            {
                reason = reason + ": " + info.ExceptionMessage;
            }
            statusLabel.Text = reason;
            outputPanel.AppendLine("Debugger stopped: " + reason);
            SelectBottomTab("CallStack");
            var frame = info.CallStack.FirstOrDefault(f => !string.IsNullOrEmpty(f.File));
            if (frame != null)
            {
                NavigateTo(frame.File, frame.Line, 1);
                EditorTab tab;
                if (_tabsByPath.TryGetValue(Path.GetFullPath(frame.File), out tab))
                {
                    tab.Editor.SetDebugLine(frame.Line);
                }
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
            var location = await _navigation.GoToDefinitionAsync(tab.Doc.FilePath, tab.Editor.InnerEditor.CaretOffset);
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
            var locations = await _navigation.FindReferencesAsync(tab.Doc.FilePath, tab.Editor.InnerEditor.CaretOffset);
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
            var line = tab.Doc.Text.GetLineByOffset(tab.Editor.InnerEditor.CaretOffset);
            var text = tab.Doc.Text.GetText(line.Offset, line.Length);
            var column = tab.Editor.InnerEditor.CaretOffset - line.Offset;
            int start = column;
            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
            {
                start--;
            }
            int end = column;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
            {
                end++;
            }
            return end > start ? text.Substring(start, end - start) : null;
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
                        var lineObj = tab.Doc.Text.GetLineByNumber(line);
                        var offset = lineObj.Offset + Math.Min(column - 1, lineObj.Length);
                        tab.Editor.InnerEditor.CaretOffset = offset;
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
            var text = Microsoft.VisualBasic.Interaction.InputBox("Line number (1-" + tab.Doc.Text.LineCount + "):", "Go To Line", "1");
            int line;
            if (int.TryParse(text, out line) && line >= 1)
            {
                tab.Editor.GotoLine(Math.Min(line, tab.Doc.Text.LineCount));
            }
        }

        private void ToggleBreakpointClick(object sender, EventArgs e)
        {
            var tab = ActiveTab;
            if (tab == null)
            {
                return;
            }
            int line = tab.Editor.InnerEditor.TextArea.Caret.Line;
            if (!tab.Doc.BreakpointLines.Remove(line))
            {
                tab.Doc.BreakpointLines.Add(line);
            }
            tab.Editor.InnerEditor.TextArea.TextView.Redraw();
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
                tab.Editor.InnerEditor.TextArea.TextView.Redraw();
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
            File.WriteAllText(tab.Doc.FilePath, tab.Doc.Text.Text);
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
                    File.WriteAllText(tab.Doc.FilePath, tab.Doc.Text.Text);
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
                Directory.CreateDirectory(dialog.ProjectDirectory);
                ProjectBase project;
                if (dialog.Language == DevLanguage.CSharp)
                {
                    var csprojPath = Path.Combine(dialog.ProjectDirectory, dialog.ProjectName + ".csproj");
                    project = CSharpProject.CreateNew(csprojPath, dialog.ProjectName, OutputKindEx.ConsoleApplication);
                    var programPath = Path.Combine(dialog.ProjectDirectory, "Program.cs");
                    if (!File.Exists(programPath))
                    {
                        File.WriteAllText(programPath, NewCSharpTemplate(dialog.ProjectName));
                    }
                    ((CSharpProject)project).AddFile(programPath);
                    project.Save();
                    WorkspaceManager.Instance.LoadProject((CSharpProject)project);
                }
                else
                {
                    var vcxprojPath = Path.Combine(dialog.ProjectDirectory, dialog.ProjectName + ".vcxproj");
                    project = CppProjectFactory.CreateNew(vcxprojPath, dialog.ProjectName);
                    var mainPath = Path.Combine(dialog.ProjectDirectory, "main.cpp");
                    if (!File.Exists(mainPath))
                    {
                        File.WriteAllText(mainPath, NewCppTemplate());
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
                foreach (var item in project.SourceFiles)
                {
                    OpenFile(item.FullPath);
                }
                return;
            }
            _projects.Add(project);
            solutionExplorer.LoadProject(project);
            foreach (var item in project.SourceFiles)
            {
                OpenFile(item.FullPath);
            }
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
            solutionExplorer.RefreshProject(project);
            EditorTab tab;
            if (_tabsByPath.TryGetValue(item.FullPath, out tab))
            {
                CloseTab(tab);
            }
        }

        private void RemoveProject(ProjectBase project)
        {
            _projects.Remove(project);
            if (project is CSharpProject)
            {
                foreach (var item in project.Items)
                {
                    WorkspaceManager.Instance.RemoveDocument(item.FullPath);
                }
            }
            if (ActiveProject != null)
            {
                Text = "DevStudio - " + ActiveProject.Name;
            }
            else
            {
                Text = "DevStudio";
            }
            UpdateDebugUi();
        }

        private void CloseTab(EditorTab tab)
        {
            _tabs.Remove(tab);
            _tabsByPath.Remove(tab.Doc.FilePath);
            editorTabs.TabPages.Remove(tab.Page);
            tab.Page.Controls.Clear();
            tab.Editor.Dispose();
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
            MessageBox.Show(this,
                "DevStudio\n\nA lightweight development studio for C# and C/C++.\n\n" +
                "C#: Roslyn editor services, in-process compiler, ICorDebug debugger,\nWinForms designer, DLL references.\n\n" +
                "C/C++: external MinGW (g++) compiler.\n\n" +
                "Shortcuts: F5 Start, F6 Build, F9 Breakpoint, F10 Step Over, F11 Step Into,\nCtrl+F Find in Files, Ctrl+G Go To Line.",
                "About DevStudio", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
    }
}
