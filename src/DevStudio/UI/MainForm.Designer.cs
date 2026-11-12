namespace DevStudio.UI
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.SplitContainer mainSplit;
        private System.Windows.Forms.SplitContainer bottomSplit;
        private System.Windows.Forms.TabControl editorTabs;
        private System.Windows.Forms.TabControl bottomTabs;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.ToolStripStatusLabel buildStatusLabel;
        private Panels.SolutionExplorerPanel solutionExplorer;

        private System.Windows.Forms.ToolStripMenuItem fileMenu;
        private System.Windows.Forms.ToolStripMenuItem newProjectMenu;
        private System.Windows.Forms.ToolStripMenuItem newFileMenu;
        private System.Windows.Forms.ToolStripMenuItem openProjectMenu;
        private System.Windows.Forms.ToolStripMenuItem openSingleFileMenu;
        private System.Windows.Forms.ToolStripMenuItem saveMenu;
        private System.Windows.Forms.ToolStripMenuItem saveAllMenu;
        private System.Windows.Forms.ToolStripMenuItem exitMenu;
        private System.Windows.Forms.ToolStripMenuItem editMenu;
        private System.Windows.Forms.ToolStripMenuItem findMenu;
        private System.Windows.Forms.ToolStripMenuItem goToLineMenu;
        private System.Windows.Forms.ToolStripMenuItem viewMenu;
        private System.Windows.Forms.ToolStripMenuItem themeMenu;
        private System.Windows.Forms.ToolStripMenuItem toggleMinimapMenu;
        private System.Windows.Forms.ToolStripMenuItem buildMenu;
        private System.Windows.Forms.ToolStripMenuItem buildSolutionMenu;
        private System.Windows.Forms.ToolStripMenuItem rebuildSolutionMenu;
        private System.Windows.Forms.ToolStripMenuItem debugMenu;
        private System.Windows.Forms.ToolStripMenuItem startDebugMenu;
        private System.Windows.Forms.ToolStripMenuItem startWithoutDebugMenu;
        private System.Windows.Forms.ToolStripMenuItem stopDebugMenu;
        private System.Windows.Forms.ToolStripMenuItem stepOverMenu;
        private System.Windows.Forms.ToolStripMenuItem stepIntoMenu;
        private System.Windows.Forms.ToolStripMenuItem stepOutMenu;
        private System.Windows.Forms.ToolStripMenuItem toggleBreakpointMenu;
        private System.Windows.Forms.ToolStripMenuItem toolsMenu;
        private System.Windows.Forms.ToolStripMenuItem addReferenceMenu;
        private System.Windows.Forms.ToolStripMenuItem helpMenu;
        private System.Windows.Forms.ToolStripMenuItem aboutMenu;

        private System.Windows.Forms.ToolStripButton newButton;
        private System.Windows.Forms.ToolStripButton openButton;
        private System.Windows.Forms.ToolStripButton saveButton;
        private System.Windows.Forms.ToolStripButton buildButton;
        private System.Windows.Forms.ToolStripButton startButton;
        private System.Windows.Forms.ToolStripButton stopButton;
        private System.Windows.Forms.ToolStripButton stepOverButton;
        private System.Windows.Forms.ToolStripButton stepIntoButton;
        private System.Windows.Forms.ToolStripButton stepOutButton;
        private System.Windows.Forms.ToolStripComboBox platformCombo;
        private System.Windows.Forms.ToolStripComboBox configCombo;

        private Panels.OutputPanel outputPanel;
        private Panels.ErrorListPanel errorListPanel;
        private Panels.FindResultsPanel findResultsPanel;
        private Panels.CallStackPanel callStackPanel;
        private Panels.BreakpointsPanel breakpointsPanel;
        private Panels.LocalsPanel localsPanel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.mainSplit = new System.Windows.Forms.SplitContainer();
            this.bottomSplit = new System.Windows.Forms.SplitContainer();
            this.editorTabs = new System.Windows.Forms.TabControl();
            this.bottomTabs = new System.Windows.Forms.TabControl();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.buildStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.solutionExplorer = new Panels.SolutionExplorerPanel();
            this.outputPanel = new Panels.OutputPanel();
            this.errorListPanel = new Panels.ErrorListPanel();
            this.findResultsPanel = new Panels.FindResultsPanel();
            this.callStackPanel = new Panels.CallStackPanel();
            this.breakpointsPanel = new Panels.BreakpointsPanel();
            this.localsPanel = new Panels.LocalsPanel();

            this.fileMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.newProjectMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.newFileMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.openProjectMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.openSingleFileMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.saveMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.saveAllMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.exitMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.editMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.findMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.goToLineMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.viewMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.themeMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.toggleMinimapMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.buildMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.buildSolutionMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.rebuildSolutionMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.debugMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.startDebugMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.startWithoutDebugMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.stopDebugMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.stepOverMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.stepIntoMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.stepOutMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.toggleBreakpointMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.addReferenceMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.helpMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutMenu = new System.Windows.Forms.ToolStripMenuItem();

            this.newButton = new System.Windows.Forms.ToolStripButton();
            this.openButton = new System.Windows.Forms.ToolStripButton();
            this.saveButton = new System.Windows.Forms.ToolStripButton();
            this.buildButton = new System.Windows.Forms.ToolStripButton();
            this.startButton = new System.Windows.Forms.ToolStripButton();
            this.stopButton = new System.Windows.Forms.ToolStripButton();
            this.stepOverButton = new System.Windows.Forms.ToolStripButton();
            this.stepIntoButton = new System.Windows.Forms.ToolStripButton();
            this.stepOutButton = new System.Windows.Forms.ToolStripButton();
            this.platformCombo = new System.Windows.Forms.ToolStripComboBox();
            this.configCombo = new System.Windows.Forms.ToolStripComboBox();

            this.menuStrip.SuspendLayout();
            this.toolStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.mainSplit)).BeginInit();
            this.mainSplit.Panel1.SuspendLayout();
            this.mainSplit.Panel2.SuspendLayout();
            this.mainSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bottomSplit)).BeginInit();
            this.bottomSplit.Panel1.SuspendLayout();
            this.bottomSplit.Panel2.SuspendLayout();
            this.bottomSplit.SuspendLayout();
            this.editorTabs.SuspendLayout();
            this.bottomTabs.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            //
            // menuStrip
            //
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileMenu,
            this.editMenu,
            this.viewMenu,
            this.buildMenu,
            this.debugMenu,
            this.toolsMenu,
            this.helpMenu});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(1280, 24);
            this.menuStrip.TabIndex = 0;
            //
            // fileMenu
            //
            this.fileMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newProjectMenu,
            this.newFileMenu,
            this.openProjectMenu,
            this.openSingleFileMenu,
            new System.Windows.Forms.ToolStripSeparator(),
            this.saveMenu,
            this.saveAllMenu,
            new System.Windows.Forms.ToolStripSeparator(),
            this.exitMenu});
            this.fileMenu.Name = "fileMenu";
            this.fileMenu.Text = "&File";
            //
            // newProjectMenu
            //
            this.newProjectMenu.Name = "newProjectMenu";
            this.newProjectMenu.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.N)));
            this.newProjectMenu.Text = "New Project...";
            this.newProjectMenu.Click += new System.EventHandler(this.NewProjectClick);
            //
            // newFileMenu
            //
            this.newFileMenu.Name = "newFileMenu";
            this.newFileMenu.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.N)));
            this.newFileMenu.Text = "New File...";
            this.newFileMenu.Click += new System.EventHandler(this.NewFileClick);
            //
            // openProjectMenu
            //
            this.openProjectMenu.Name = "openProjectMenu";
            this.openProjectMenu.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.O)));
            this.openProjectMenu.Text = "Open Project...";
            this.openProjectMenu.Click += new System.EventHandler(this.OpenProjectClick);
            //
            // openSingleFileMenu
            //
            this.openSingleFileMenu.Name = "openSingleFileMenu";
            this.openSingleFileMenu.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.openSingleFileMenu.Text = "Open Single File...";
            this.openSingleFileMenu.Click += new System.EventHandler(this.OpenSingleFileClick);
            //
            // saveMenu
            //
            this.saveMenu.Name = "saveMenu";
            this.saveMenu.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.saveMenu.Text = "Save";
            this.saveMenu.Click += new System.EventHandler(this.SaveClick);
            //
            // saveAllMenu
            //
            this.saveAllMenu.Name = "saveAllMenu";
            this.saveAllMenu.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.S)));
            this.saveAllMenu.Text = "Save All";
            this.saveAllMenu.Click += new System.EventHandler(this.SaveAllClick);
            //
            // exitMenu
            //
            this.exitMenu.Name = "exitMenu";
            this.exitMenu.Text = "Exit";
            this.exitMenu.Click += new System.EventHandler(this.ExitClick);
            //
            // editMenu
            //
            this.editMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.findMenu,
            this.goToLineMenu});
            this.editMenu.Name = "editMenu";
            this.editMenu.Text = "&Edit";
            //
            // findMenu
            //
            this.findMenu.Name = "findMenu";
            this.findMenu.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F)));
            this.findMenu.Text = "Find in Files...";
            this.findMenu.Click += new System.EventHandler(this.FindClick);
            //
            // goToLineMenu
            //
            this.goToLineMenu.Name = "goToLineMenu";
            this.goToLineMenu.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.G)));
            this.goToLineMenu.Text = "Go To Line...";
            this.goToLineMenu.Click += new System.EventHandler(this.GoToLineClick);
            //
            // viewMenu
            //
            this.viewMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.themeMenu,
            this.toggleMinimapMenu});
            this.viewMenu.Name = "viewMenu";
            this.viewMenu.Text = "&View";
            //
            // themeMenu
            //
            this.themeMenu.Name = "themeMenu";
            this.themeMenu.Text = "Color Theme";
            //
            // toggleMinimapMenu
            //
            this.toggleMinimapMenu.Name = "toggleMinimapMenu";
            this.toggleMinimapMenu.Text = "Toggle Minimap";
            this.toggleMinimapMenu.Click += new System.EventHandler(this.ToggleMinimapClick);
            //
            // buildMenu
            //
            this.buildMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.buildSolutionMenu,
            this.rebuildSolutionMenu});
            this.buildMenu.Name = "buildMenu";
            this.buildMenu.Text = "&Build";
            //
            // buildSolutionMenu
            //
            this.buildSolutionMenu.Name = "buildSolutionMenu";
            this.buildSolutionMenu.ShortcutKeys = System.Windows.Forms.Keys.F6;
            this.buildSolutionMenu.Text = "Build Solution";
            this.buildSolutionMenu.Click += new System.EventHandler(this.BuildClick);
            //
            // rebuildSolutionMenu
            //
            this.rebuildSolutionMenu.Name = "rebuildSolutionMenu";
            this.rebuildSolutionMenu.Text = "Rebuild Solution";
            this.rebuildSolutionMenu.Click += new System.EventHandler(this.RebuildClick);
            //
            // debugMenu
            //
            this.debugMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.startDebugMenu,
            this.startWithoutDebugMenu,
            this.stopDebugMenu,
            new System.Windows.Forms.ToolStripSeparator(),
            this.stepOverMenu,
            this.stepIntoMenu,
            this.stepOutMenu,
            new System.Windows.Forms.ToolStripSeparator(),
            this.toggleBreakpointMenu});
            this.debugMenu.Name = "debugMenu";
            this.debugMenu.Text = "&Debug";
            //
            // startDebugMenu
            //
            this.startDebugMenu.Name = "startDebugMenu";
            this.startDebugMenu.ShortcutKeys = System.Windows.Forms.Keys.F5;
            this.startDebugMenu.Text = "Start Debugging";
            this.startDebugMenu.Click += new System.EventHandler(this.StartDebugClick);
            //
            // startWithoutDebugMenu
            //
            this.startWithoutDebugMenu.Name = "startWithoutDebugMenu";
            this.startWithoutDebugMenu.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.F5)));
            this.startWithoutDebugMenu.Text = "Start Without Debugging";
            this.startWithoutDebugMenu.Click += new System.EventHandler(this.StartWithoutDebugClick);
            //
            // stopDebugMenu
            //
            this.stopDebugMenu.Name = "stopDebugMenu";
            this.stopDebugMenu.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.F5)));
            this.stopDebugMenu.Text = "Stop Debugging";
            this.stopDebugMenu.Click += new System.EventHandler(this.StopDebugClick);
            //
            // stepOverMenu
            //
            this.stepOverMenu.Name = "stepOverMenu";
            this.stepOverMenu.ShortcutKeys = System.Windows.Forms.Keys.F10;
            this.stepOverMenu.Text = "Step Over";
            this.stepOverMenu.Click += new System.EventHandler(this.StepOverClick);
            //
            // stepIntoMenu
            //
            this.stepIntoMenu.Name = "stepIntoMenu";
            this.stepIntoMenu.ShortcutKeys = System.Windows.Forms.Keys.F11;
            this.stepIntoMenu.Text = "Step Into";
            this.stepIntoMenu.Click += new System.EventHandler(this.StepIntoClick);
            //
            // stepOutMenu
            //
            this.stepOutMenu.Name = "stepOutMenu";
            this.stepOutMenu.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Shift | System.Windows.Forms.Keys.F11)));
            this.stepOutMenu.Text = "Step Out";
            this.stepOutMenu.Click += new System.EventHandler(this.StepOutClick);
            //
            // toggleBreakpointMenu
            //
            this.toggleBreakpointMenu.Name = "toggleBreakpointMenu";
            this.toggleBreakpointMenu.ShortcutKeys = System.Windows.Forms.Keys.F9;
            this.toggleBreakpointMenu.Text = "Toggle Breakpoint";
            this.toggleBreakpointMenu.Click += new System.EventHandler(this.ToggleBreakpointClick);
            //
            // toolsMenu
            //
            this.toolsMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addReferenceMenu});
            this.toolsMenu.Name = "toolsMenu";
            this.toolsMenu.Text = "&Tools";
            //
            // addReferenceMenu
            //
            this.addReferenceMenu.Name = "addReferenceMenu";
            this.addReferenceMenu.Text = "Add DLL Reference...";
            this.addReferenceMenu.Click += new System.EventHandler(this.AddReferenceClick);
            //
            // helpMenu
            //
            this.helpMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutMenu});
            this.helpMenu.Name = "helpMenu";
            this.helpMenu.Text = "&Help";
            //
            // aboutMenu
            //
            this.aboutMenu.Name = "aboutMenu";
            this.aboutMenu.Text = "About DevStudio";
            this.aboutMenu.Click += new System.EventHandler(this.AboutClick);
            //
            // toolStrip
            //
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newButton,
            this.openButton,
            this.saveButton,
            new System.Windows.Forms.ToolStripSeparator(),
            this.buildButton,
            new System.Windows.Forms.ToolStripSeparator(),
            this.startButton,
            this.stopButton,
            this.stepOverButton,
            this.stepIntoButton,
            this.stepOutButton,
            new System.Windows.Forms.ToolStripSeparator(),
            this.configCombo,
            this.platformCombo});
            this.toolStrip.Location = new System.Drawing.Point(0, 24);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(1280, 25);
            this.toolStrip.TabIndex = 1;
            //
            // newButton
            //
            this.newButton.Name = "newButton";
            this.newButton.Text = "New";
            this.newButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.newButton.Click += new System.EventHandler(this.NewFileClick);
            //
            // openButton
            //
            this.openButton.Name = "openButton";
            this.openButton.Text = "Open";
            this.openButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.openButton.Click += new System.EventHandler(this.OpenProjectClick);
            //
            // saveButton
            //
            this.saveButton.Name = "saveButton";
            this.saveButton.Text = "Save";
            this.saveButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.saveButton.Click += new System.EventHandler(this.SaveClick);
            //
            // buildButton
            //
            this.buildButton.Name = "buildButton";
            this.buildButton.Text = "Build";
            this.buildButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.buildButton.Click += new System.EventHandler(this.BuildClick);
            //
            // startButton
            //
            this.startButton.Name = "startButton";
            this.startButton.Text = "Start";
            this.startButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.startButton.Click += new System.EventHandler(this.StartDebugClick);
            //
            // stopButton
            //
            this.stopButton.Name = "stopButton";
            this.stopButton.Text = "Stop";
            this.stopButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.stopButton.Click += new System.EventHandler(this.StopDebugClick);
            //
            // stepOverButton
            //
            this.stepOverButton.Name = "stepOverButton";
            this.stepOverButton.Text = "Step Over";
            this.stepOverButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.stepOverButton.Click += new System.EventHandler(this.StepOverClick);
            //
            // stepIntoButton
            //
            this.stepIntoButton.Name = "stepIntoButton";
            this.stepIntoButton.Text = "Step Into";
            this.stepIntoButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.stepIntoButton.Click += new System.EventHandler(this.StepIntoClick);
            //
            // stepOutButton
            //
            this.stepOutButton.Name = "stepOutButton";
            this.stepOutButton.Text = "Step Out";
            this.stepOutButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.stepOutButton.Click += new System.EventHandler(this.StepOutClick);
            //
            // configCombo
            //
            this.configCombo.Name = "configCombo";
            this.configCombo.Items.AddRange(new object[] { "Debug", "Release" });
            this.configCombo.SelectedIndex = 0;
            this.configCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.configCombo.ToolTipText = "Configuration";
            //
            // platformCombo
            //
            this.platformCombo.Name = "platformCombo";
            this.platformCombo.Items.AddRange(new object[] { "AnyCPU", "x86", "x64" });
            this.platformCombo.SelectedIndex = 0;
            this.platformCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.platformCombo.ToolTipText = "Platform";
            this.platformCombo.SelectedIndexChanged += new System.EventHandler(this.PlatformChanged);
            //
            // mainSplit
            //
            this.mainSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainSplit.Location = new System.Drawing.Point(0, 49);
            this.mainSplit.Name = "mainSplit";
            this.mainSplit.Panel1.Controls.Add(this.solutionExplorer);
            this.mainSplit.Panel2.Controls.Add(this.bottomSplit);
            this.mainSplit.Size = new System.Drawing.Size(1280, 671);
            this.mainSplit.SplitterDistance = 250;
            this.mainSplit.TabIndex = 2;
            //
            // solutionExplorer
            //
            this.solutionExplorer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.solutionExplorer.Location = new System.Drawing.Point(0, 0);
            this.solutionExplorer.Name = "solutionExplorer";
            this.solutionExplorer.Size = new System.Drawing.Size(250, 671);
            this.solutionExplorer.TabIndex = 0;
            //
            // bottomSplit
            //
            this.bottomSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bottomSplit.Location = new System.Drawing.Point(0, 0);
            this.bottomSplit.Name = "bottomSplit";
            this.bottomSplit.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.bottomSplit.Panel1.Controls.Add(this.editorTabs);
            this.bottomSplit.Panel2.Controls.Add(this.bottomTabs);
            this.bottomSplit.Size = new System.Drawing.Size(1026, 671);
            this.bottomSplit.SplitterDistance = 480;
            this.bottomSplit.TabIndex = 0;
            //
            // editorTabs
            //
            this.editorTabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editorTabs.Location = new System.Drawing.Point(0, 0);
            this.editorTabs.Name = "editorTabs";
            this.editorTabs.SelectedIndex = 0;
            this.editorTabs.Size = new System.Drawing.Size(1026, 480);
            this.editorTabs.TabIndex = 0;
            //
            // bottomTabs
            //
            this.bottomTabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bottomTabs.Location = new System.Drawing.Point(0, 0);
            this.bottomTabs.Name = "bottomTabs";
            this.bottomTabs.SelectedIndex = 0;
            this.bottomTabs.Size = new System.Drawing.Size(1026, 187);
            this.bottomTabs.TabIndex = 0;
            this.bottomTabs.TabPages.Add(CreateBottomTab("Output", this.outputPanel));
            this.bottomTabs.TabPages.Add(CreateBottomTab("Error List", this.errorListPanel));
            this.bottomTabs.TabPages.Add(CreateBottomTab("Find Results", this.findResultsPanel));
            this.bottomTabs.TabPages.Add(CreateBottomTab("Call Stack", this.callStackPanel));
            this.bottomTabs.TabPages.Add(CreateBottomTab("Breakpoints", this.breakpointsPanel));
            this.bottomTabs.TabPages.Add(CreateBottomTab("Locals", this.localsPanel));
            //
            // statusStrip
            //
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel,
            this.buildStatusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 720);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1280, 22);
            this.statusStrip.TabIndex = 3;
            //
            // statusLabel
            //
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Text = "Ready";
            this.statusLabel.Spring = true;
            this.statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // buildStatusLabel
            //
            this.buildStatusLabel.Name = "buildStatusLabel";
            this.buildStatusLabel.Text = "";
            this.buildStatusLabel.AutoSize = true;
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1280, 742);
            this.Controls.Add(this.mainSplit);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.menuStrip);
            this.Controls.Add(this.statusStrip);
            this.MainMenuStrip = this.menuStrip;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "DevStudio";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainFormFormClosing);
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.mainSplit.Panel1.ResumeLayout(false);
            this.mainSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.mainSplit)).EndInit();
            this.mainSplit.ResumeLayout(false);
            this.bottomSplit.Panel1.ResumeLayout(false);
            this.bottomSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.bottomSplit)).EndInit();
            this.bottomSplit.ResumeLayout(false);
            this.editorTabs.ResumeLayout(false);
            this.bottomTabs.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.TabPage CreateBottomTab(string title, System.Windows.Forms.Control content)
        {
            var page = new System.Windows.Forms.TabPage();
            page.Name = title.Replace(" ", "");
            page.Text = title;
            content.Dock = System.Windows.Forms.DockStyle.Fill;
            page.Controls.Add(content);
            return page;
        }
    }
}
