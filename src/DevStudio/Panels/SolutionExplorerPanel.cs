using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DevStudio.Projects;
using DevStudio.Theming;
using DevStudio.UI;

namespace DevStudio.Panels
{
    public partial class SolutionExplorerPanel : UserControl
    {
        public event Action<string> FileOpenRequested;
        public event Action<ProjectBase> AddFileRequested;
        public event Action<ProjectBase> AddNewFileRequested;
        public event Action<ProjectBase> AddReferenceRequested;
        public event Action<ProjectItem> FileRemoved;
        public event Action<ProjectBase> ProjectRemoved;

        private ToolStripMenuItem _openItem;
        private ToolStripMenuItem _addExistingItem;
        private ToolStripMenuItem _addNewItem;
        private ToolStripMenuItem _addReferenceItem;
        private ToolStripMenuItem _removeFileItem;
        private ToolStripMenuItem _setStartupItem;
        private ToolStripMenuItem _removeProjectItem;
        private ToolStripMenuItem _closeProjectItem;

        public ProjectBase StartupProject { get; private set; }

        public SolutionExplorerPanel()
        {
            InitializeComponent();
            projectTree.ImageList = StudioIcons.CreateTreeImageList();
            projectTree.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var node = projectTree.GetNodeAt(e.X, e.Y);
                    if (node != null)
                    {
                        projectTree.SelectedNode = node;
                    }
                }
            };
            BuildContextMenu();
        }

        public void LoadProject(ProjectBase project)
        {
            var node = CreateProjectNode(project);
            projectTree.Nodes.Add(node);
            if (StartupProject == null)
            {
                SetStartup(project);
            }
        }

        public void ClearAll()
        {
            projectTree.Nodes.Clear();
            StartupProject = null;
        }

        public void RemoveProject(ProjectBase project)
        {
            for (int i = 0; i < projectTree.Nodes.Count; i++)
            {
                if (ReferenceEquals(projectTree.Nodes[i].Tag, project))
                {
                    projectTree.Nodes.RemoveAt(i);
                    break;
                }
            }
            if (ReferenceEquals(StartupProject, project))
            {
                StartupProject = projectTree.Nodes.Count > 0 ? projectTree.Nodes[0].Tag as ProjectBase : null;
                if (StartupProject != null)
                {
                    SetStartup(StartupProject);
                }
            }
        }

        private TreeNode CreateProjectNode(ProjectBase project)
        {
            var node = new TreeNode(project.Name)
            {
                Tag = project,
                ImageKey = project.Language == DevLanguage.CSharp ? "ProjectCs" : "ProjectCpp",
                SelectedImageKey = project.Language == DevLanguage.CSharp ? "ProjectCs" : "ProjectCpp"
            };
            foreach (var item in project.Items)
            {
                string key = GetFileIconKey(item.DisplayName);
                node.Nodes.Add(new TreeNode(item.DisplayName)
                {
                    Tag = item,
                    ImageKey = key,
                    SelectedImageKey = key
                });
            }
            var referencesNode = new TreeNode("References")
            {
                ImageKey = "References",
                SelectedImageKey = "References"
            };
            foreach (var reference in project.References)
            {
                referencesNode.Nodes.Add(new TreeNode(reference.ToString())
                {
                    ImageKey = "References",
                    SelectedImageKey = "References"
                });
            }
            if (referencesNode.Nodes.Count > 0)
            {
                node.Nodes.Add(referencesNode);
            }
            node.Expand();
            return node;
        }

        private static string GetFileIconKey(string fileName)
        {
            string ext = System.IO.Path.GetExtension(fileName ?? "").ToLowerInvariant();
            switch (ext)
            {
                case ".cs": return "FileCs";
                case ".cpp":
                case ".cc":
                case ".cxx": return "FileCpp";
                case ".c": return "FileC";
                case ".h":
                case ".hpp": return "FileH";
                case ".csproj": return "FileCsproj";
                case ".vcxproj": return "FileVcxproj";
                default: return "FileOther";
            }
        }

        public void RefreshProject(ProjectBase project)
        {
            for (int i = 0; i < projectTree.Nodes.Count; i++)
            {
                if (ReferenceEquals(projectTree.Nodes[i].Tag, project))
                {
                    bool expanded = projectTree.Nodes[i].IsExpanded;
                    projectTree.Nodes.RemoveAt(i);
                    var node = CreateProjectNode(project);
                    projectTree.Nodes.Insert(i, node);
                    if (ReferenceEquals(StartupProject, project))
                    {
                        node.BackColor = UiTheme.Accent;
                        node.ForeColor = Color.White;
                    }
                    if (expanded)
                    {
                        node.Expand();
                    }
                    break;
                }
            }
        }

        private void BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            _openItem = new ToolStripMenuItem("Open");
            _openItem.Click += (s, e) => WithFileNode(FileOpenRequested);

            _addExistingItem = new ToolStripMenuItem("Add Existing File...");
            _addExistingItem.Click += (s, e) => WithProjectNode(AddFileRequested);

            _addNewItem = new ToolStripMenuItem("Add New File...");
            _addNewItem.Click += (s, e) => WithProjectNode(AddNewFileRequested);

            _addReferenceItem = new ToolStripMenuItem("Add DLL Reference...");
            _addReferenceItem.Click += (s, e) => WithProjectNode(AddReferenceRequested);

            _removeFileItem = new ToolStripMenuItem("Remove From Project");
            _removeFileItem.Click += (s, e) =>
            {
                if (projectTree.SelectedNode != null && projectTree.SelectedNode.Tag is ProjectItem)
                {
                    var item = (ProjectItem)projectTree.SelectedNode.Tag;
                    projectTree.SelectedNode.Remove();
                    FileRemoved?.Invoke(item);
                }
            };

            _setStartupItem = new ToolStripMenuItem("Set as Startup Project");
            _setStartupItem.Click += (s, e) =>
            {
                if (projectTree.SelectedNode != null && projectTree.SelectedNode.Tag is ProjectBase)
                {
                    SetStartup((ProjectBase)projectTree.SelectedNode.Tag);
                }
            };

            _removeProjectItem = new ToolStripMenuItem("Remove Project");
            _removeProjectItem.Click += (s, e) =>
            {
                if (projectTree.SelectedNode != null && projectTree.SelectedNode.Tag is ProjectBase)
                {
                    ProjectRemoved?.Invoke((ProjectBase)projectTree.SelectedNode.Tag);
                }
            };

            _closeProjectItem = new ToolStripMenuItem("Close Project");
            _closeProjectItem.Click += (s, e) =>
            {
                if (projectTree.SelectedNode != null && projectTree.SelectedNode.Tag is ProjectBase)
                {
                    ProjectRemoved?.Invoke((ProjectBase)projectTree.SelectedNode.Tag);
                }
            };

            menu.Items.Add(_openItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_addExistingItem);
            menu.Items.Add(_addNewItem);
            menu.Items.Add(_addReferenceItem);
            menu.Items.Add(_removeFileItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_setStartupItem);
            menu.Items.Add(_closeProjectItem);
            menu.Items.Add(_removeProjectItem);

            menu.Opening += MenuOpening;
            projectTree.ContextMenuStrip = menu;
        }

        private void MenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_openItem == null)
            {
                return;
            }
            var node = projectTree.SelectedNode;
            bool isFile = node != null && node.Tag is ProjectItem;
            ProjectBase project = null;
            var walk = node;
            while (walk != null && project == null)
            {
                project = walk.Tag as ProjectBase;
                walk = walk.Parent;
            }
            bool isProject = node != null && node.Tag is ProjectBase;
            _openItem.Enabled = isFile;
            _addExistingItem.Enabled = isProject;
            _addNewItem.Enabled = isProject;
            _addReferenceItem.Enabled = project != null && project.Language == DevLanguage.CSharp
                && (isProject || IsReferencesContainer(node));
            _removeFileItem.Enabled = isFile;
            _setStartupItem.Enabled = isProject;
            _closeProjectItem.Enabled = isProject;
            _removeProjectItem.Enabled = isProject;
        }

        private static bool IsReferencesContainer(TreeNode node)
        {
            for (var current = node; current != null; current = current.Parent)
            {
                if (current.Tag == null && current.Text == "References")
                {
                    return true;
                }
                if (current.Tag is ProjectBase)
                {
                    return false;
                }
            }
            return false;
        }

        private void WithFileNode(Action<string> action)
        {
            if (projectTree.SelectedNode != null && projectTree.SelectedNode.Tag is ProjectItem)
            {
                action?.Invoke(((ProjectItem)projectTree.SelectedNode.Tag).FullPath);
            }
        }

        private void WithProjectNode(Action<ProjectBase> action)
        {
            ProjectBase project = null;
            var node = projectTree.SelectedNode;
            while (node != null && project == null)
            {
                project = node.Tag as ProjectBase;
                node = node.Parent;
            }
            if (project != null)
            {
                action?.Invoke(project);
            }
        }

        public void SetStartup(ProjectBase project)
        {
            StartupProject = project;
            foreach (TreeNode node in projectTree.Nodes)
            {
                if (ReferenceEquals(node.Tag, project))
                {
                    node.BackColor = UiTheme.Accent;
                    node.ForeColor = Color.White;
                }
                else
                {
                    node.BackColor = Color.Empty;
                    node.ForeColor = UiTheme.Text;
                }
            }
        }

        private void ProjectTreeDoubleClick(object sender, EventArgs e)
        {
            WithFileNode(FileOpenRequested);
        }
    }
}
