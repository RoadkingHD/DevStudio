using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DevStudio.Projects;

namespace DevStudio.Dialogs
{
    public partial class AddReferenceDialog : Form
    {
        public AssemblyReference SelectedReference { get; private set; }

        public AddReferenceDialog()
        {
            InitializeComponent();
        }

        private void BrowseFileClick(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Select Assembly",
                Filter = ".NET Assemblies (*.dll)|*.dll|Executables (*.exe)|*.exe|All Files (*.*)|*.*"
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    pathBox.Text = dialog.FileName;
                }
            }
        }

        private void OkClick(object sender, EventArgs e)
        {
            var path = pathBox.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show(this, "Please choose a DLL file.", "Add Reference", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "The file does not exist.", "Add Reference", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SelectedReference = new AssemblyReference
            {
                AssemblyName = Path.GetFileNameWithoutExtension(path),
                HintPath = Path.GetFullPath(path)
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        private void CancelClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
