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
