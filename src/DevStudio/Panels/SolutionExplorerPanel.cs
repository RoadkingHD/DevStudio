using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DevStudio.Projects;
using DevStudio.Theming;

namespace DevStudio.Panels
{
    public partial class SolutionExplorerPanel : UserControl
    {
        public event Action<string> FileOpenRequested;
        public event Action<ProjectBase> AddFileRequested;
        public event Action<ProjectBase> AddReferenceRequested;
        public event Action<ProjectItem> FileRemoved;
        public event Action<ProjectBase> ProjectRemoved;

        public ProjectBase StartupProject { get; private set; }

        public SolutionExplorerPanel()
        {
            InitializeComponent();
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

        private TreeNode CreateProjectNode(ProjectBase project)
        {
            var node = new TreeNode(project.Name)
            {
                Tag = project
            };
            foreach (var item in project.Items)
            {
                node.Nodes.Add(new TreeNode(item.DisplayName)
                {
                    Tag = item
                });
            }
            var referencesNode = new TreeNode("References");
            foreach (var reference in project.References)
            {
                referencesNode.Nodes.Add(new TreeNode(reference.ToString()));
            }
            if (referencesNode.Nodes.Count > 0)
            {
                node.Nodes.Add(referencesNode);
            }
            node.Expand();
            return node;
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

            var open = new ToolStripMenuItem("Open");
            open.Click += (s, e) => WithFileNode(FileOpenRequested);

            var addFile = new ToolStripMenuItem("Add Existing File...");
            addFile.Click += (s, e) => WithProjectNode(AddFileRequested);

            var addRef = new ToolStripMenuItem("Add DLL Reference...");
            addRef.Click += (s, e) => WithProjectNode(AddReferenceRequested);

            var removeFile = new ToolStripMenuItem("Remove From Project");
            removeFile.Click += (s, e) =>
            {
                if (projectTree.SelectedNode != null && projectTree.SelectedNode.Tag is ProjectItem)
                {
                    var item = (ProjectItem)projectTree.SelectedNode.Tag;
                    projectTree.SelectedNode.Remove();
                    FileRemoved?.Invoke(item);
                }
            };

            var setStartup = new ToolStripMenuItem("Set as Startup Project");
            setStartup.Click += (s, e) =>
            {
                if (projectTree.SelectedNode != null && projectTree.SelectedNode.Tag is ProjectBase)
                {
                    SetStartup((ProjectBase)projectTree.SelectedNode.Tag);
                }
            };

            var removeProject = new ToolStripMenuItem("Remove Project");
            removeProject.Click += (s, e) =>
            {
                if (projectTree.SelectedNode != null && projectTree.SelectedNode.Tag is ProjectBase)
                {
                    var project = (ProjectBase)projectTree.SelectedNode.Tag;
                    projectTree.SelectedNode.Remove();
                    if (ReferenceEquals(StartupProject, project))
                    {
                        StartupProject = projectTree.Nodes.Count > 0 ? projectTree.Nodes[0].Tag as ProjectBase : null;
                    }
                    ProjectRemoved?.Invoke(project);
                }
            };

            menu.Items.Add(open);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(addFile);
            menu.Items.Add(addRef);
            menu.Items.Add(removeFile);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(setStartup);
            menu.Items.Add(removeProject);
            projectTree.ContextMenuStrip = menu;
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
