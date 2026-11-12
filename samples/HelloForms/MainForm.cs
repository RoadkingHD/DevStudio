using System;
using System.Windows.Forms;

namespace HelloForms
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void greetButton_Click(object sender, EventArgs e)
        {
            string name = string.IsNullOrWhiteSpace(nameTextBox.Text) ? "World" : nameTextBox.Text.Trim();
            greetingLabel.Text = "Hello, " + name + "!";
        }
    }
}
