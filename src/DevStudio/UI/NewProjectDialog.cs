using System;
using System.IO;
using System.Windows.Forms;
using DevStudio.Projects;

namespace DevStudio.UI
{
    public partial class NewProjectDialog : Form
    {
        private string _lastGeneratedLocation;

        public string ProjectName
        {
            get { return nameText.Text.Trim(); }
        }

        public string ProjectDirectory
        {
            get { return locationText.Text.Trim(); }
        }

        public DevLanguage Language
        {
            get { return cppRadio.Checked ? DevLanguage.Cpp : DevLanguage.CSharp; }
        }

        public OutputKindEx OutputKind
        {
            get
            {
                var template = templateCombo.SelectedItem as string ?? "";
                if (template.IndexOf("Class Library", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    template.IndexOf("Dynamic Library", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    template.IndexOf("Proxy", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return OutputKindEx.Library;
                }
                if (template.IndexOf("Windows Forms", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return OutputKindEx.WindowsApplication;
                }
                return OutputKindEx.ConsoleApplication;
            }
        }

        public bool IsProxyDll
        {
            get
            {
                var template = templateCombo.SelectedItem as string ?? "";
                return template.IndexOf("Proxy", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public NewProjectDialog()
        {
            InitializeComponent();
            nameText.TextChanged += NameTextChanged;
            browseButton.Click += BrowseClick;
            okButton.Click += OkClick;
            csharpRadio.CheckedChanged += (s, e) => PopulateTemplates();
            cppRadio.CheckedChanged += (s, e) => PopulateTemplates();
            PopulateTemplates();
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
        }

        private void PopulateTemplates()
        {
            var previous = templateCombo.SelectedItem as string;
            templateCombo.Items.Clear();
            if (cppRadio.Checked)
            {
                templateCombo.Items.AddRange(new object[]
                {
                    "Console Application",
                    "Dynamic Library (.dll)",
                    "Proxy DLL (exported stubs)"
                });
                templateCombo.SelectedIndex = 0;
            }
            else
            {
                templateCombo.Items.AddRange(new object[]
                {
                    "Console Application",
                    "Class Library (.dll)",
                    "Windows Forms Application"
                });
                templateCombo.SelectedIndex = 0;
            }
            if (previous != null)
            {
                foreach (var item in templateCombo.Items)
                {
                    if (string.Equals(item as string, previous, StringComparison.Ordinal))
                    {
                        templateCombo.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void NameTextChanged(object sender, EventArgs e)
        {
            var name = ProjectName;
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var generated = Path.Combine(documents, "DevStudio", name);
            if (string.IsNullOrEmpty(locationText.Text) || string.Equals(locationText.Text, _lastGeneratedLocation, StringComparison.OrdinalIgnoreCase))
            {
                _lastGeneratedLocation = generated;
                locationText.Text = generated;
            }
        }

        private void BrowseClick(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the directory in which to create the project.";
                if (Directory.Exists(locationText.Text))
                {
                    dialog.SelectedPath = locationText.Text;
                }
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _lastGeneratedLocation = dialog.SelectedPath;
                    locationText.Text = dialog.SelectedPath;
                }
            }
        }

        private void OkClick(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProjectName))
            {
                MessageBox.Show(this, "Enter a project name.", "New Project", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nameText.Focus();
                return;
            }
            if (ProjectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(this, "The project name contains invalid characters.", "New Project", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nameText.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(ProjectDirectory))
            {
                MessageBox.Show(this, "Enter a location for the project.", "New Project", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                locationText.Focus();
                return;
            }
            var extension = cppRadio.Checked ? ".vcxproj" : ".csproj";
            var projectFile = Path.Combine(ProjectDirectory, ProjectName + extension);
            if (File.Exists(projectFile))
            {
                MessageBox.Show(this, "A project named " + ProjectName + " already exists at this location.", "New Project", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
