using System;
using System.Windows.Forms;
using DevStudio.Projects;

namespace DevStudio.Dialogs
{
    public partial class AddNewFileDialog : Form
    {
        private readonly DevLanguage _language;
        private readonly string _projectName;

        public string FileName
        {
            get { return nameBox.Text.Trim(); }
        }

        public bool HasDesignerFile
        {
            get
            {
                var template = templateCombo.SelectedItem as string ?? "";
                return _language == DevLanguage.CSharp && template.StartsWith("Windows Form");
            }
        }

        public string GetDesignerFileContent()
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(FileName);
            var nl = Environment.NewLine;
            var sb = new System.Text.StringBuilder();
            sb.Append("using System.ComponentModel;").Append(nl);
            sb.Append("using System.Windows.Forms;").Append(nl);
            sb.Append(nl);
            sb.Append("namespace ").Append(_projectName).Append(nl);
            sb.Append("{").Append(nl);
            sb.Append("    partial class ").Append(name).Append(nl);
            sb.Append("    {").Append(nl);
            sb.Append("        private System.ComponentModel.IContainer components = null;").Append(nl);
            sb.Append(nl);
            sb.Append("        protected override void Dispose(bool disposing)").Append(nl);
            sb.Append("        {").Append(nl);
            sb.Append("            if (disposing && (components != null))").Append(nl);
            sb.Append("            {").Append(nl);
            sb.Append("                components.Dispose();").Append(nl);
            sb.Append("            }").Append(nl);
            sb.Append("            base.Dispose(disposing);").Append(nl);
            sb.Append("        }").Append(nl);
            sb.Append(nl);
            sb.Append("        #region Windows Form Designer generated code").Append(nl);
            sb.Append(nl);
            sb.Append("        private void InitializeComponent()").Append(nl);
            sb.Append("        {").Append(nl);
            sb.Append("            this.SuspendLayout();").Append(nl);
            sb.Append("            // ").Append(nl);
            sb.Append("            // ").Append(name).Append(nl);
            sb.Append("            // ").Append(nl);
            sb.Append("            this.ClientSize = new System.Drawing.Size(284, 261);").Append(nl);
            sb.Append("            this.Name = \"").Append(name).Append("\";").Append(nl);
            sb.Append("            this.Text = \"").Append(name).Append("\";").Append(nl);
            sb.Append("            this.ResumeLayout(false);").Append(nl);
            sb.Append("        }").Append(nl);
            sb.Append(nl);
            sb.Append("        #endregion").Append(nl);
            sb.Append("    }").Append(nl);
            sb.Append("}").Append(nl);
            return sb.ToString();
        }

        public AddNewFileDialog(DevLanguage language, string projectName)
        {
            _language = language;
            _projectName = projectName;
            InitializeComponent();
            if (_language == DevLanguage.Cpp)
            {
                templateCombo.Items.AddRange(new object[] { "Source File (.cpp)", "Header File (.h)", "Empty File" });
                templateCombo.SelectedIndex = 0;
                nameBox.Text = "main.cpp";
                Text = "Add New File - C/C++";
            }
            else
            {
                templateCombo.Items.AddRange(new object[] { "Class (.cs)", "Interface (.cs)", "Windows Form (.cs)", "Empty File" });
                templateCombo.SelectedIndex = 0;
                nameBox.Text = "Class1.cs";
                Text = "Add New File - C#";
            }
            nameBox.SelectAll();
        }

        private void nameBox_TextChanged(object sender, EventArgs e)
        {
            okButton.Enabled = nameBox.Text.Trim().Length > 0 && nameBox.Text.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) < 0;
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        public string GetTemplateContent()
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(FileName);
            var template = templateCombo.SelectedItem as string ?? "";
            if (_language == DevLanguage.Cpp)
            {
                if (template.StartsWith("Header")) 
                {
                    return "#pragma once" + Environment.NewLine + Environment.NewLine;
                }
                if (template.StartsWith("Source"))
                {
                    return "#include <iostream>" + Environment.NewLine + Environment.NewLine +
                           "int main()" + Environment.NewLine +
                           "{" + Environment.NewLine +
                           "    std::cout << \"Hello from " + name + "!\" << std::endl;" + Environment.NewLine +
                           "    return 0;" + Environment.NewLine +
                           "}" + Environment.NewLine;
                }
                return string.Empty;
            }
            if (template.StartsWith("Interface"))
            {
                return "using System;" + Environment.NewLine + Environment.NewLine +
                       "namespace " + _projectName + Environment.NewLine +
                       "{" + Environment.NewLine +
                       "    public interface I" + name + Environment.NewLine +
                       "    {" + Environment.NewLine +
                       "    }" + Environment.NewLine +
                       "}" + Environment.NewLine;
            }
            if (template.StartsWith("Windows Form"))
            {
                return "using System;" + Environment.NewLine +
                       "using System.Windows.Forms;" + Environment.NewLine + Environment.NewLine +
                       "namespace " + _projectName + Environment.NewLine +
                       "{" + Environment.NewLine +
                       "    public partial class " + name + " : Form" + Environment.NewLine +
                       "    {" + Environment.NewLine +
                       "        public " + name + "()" + Environment.NewLine +
                       "        {" + Environment.NewLine +
                       "            InitializeComponent();" + Environment.NewLine +
                       "        }" + Environment.NewLine +
                       "    }" + Environment.NewLine +
                       "}" + Environment.NewLine;
            }
            if (template.StartsWith("Class"))
            {
                return "using System;" + Environment.NewLine + Environment.NewLine +
                       "namespace " + _projectName + Environment.NewLine +
                       "{" + Environment.NewLine +
                       "    public class " + name + Environment.NewLine +
                       "    {" + Environment.NewLine +
                       "    }" + Environment.NewLine +
                       "}" + Environment.NewLine;
            }
            return string.Empty;
        }
    }
}
