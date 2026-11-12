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

        public NewProjectDialog()
        {
            InitializeComponent();
            nameText.TextChanged += NameTextChanged;
            browseButton.Click += BrowseClick;
            okButton.Click += OkClick;
            cancelButton.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
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
