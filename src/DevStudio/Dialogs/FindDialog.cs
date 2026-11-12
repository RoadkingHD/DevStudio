using System;
using System.Windows.Forms;

namespace DevStudio.Dialogs
{
    public partial class FindDialog : Form
    {
        public string Query
        {
            get { return queryBox.Text; }
        }

        public bool MatchCase
        {
            get { return matchCaseCheck.Checked; }
        }

        public bool WholeWord
        {
            get { return wholeWordCheck.Checked; }
        }

        public bool UseRegex
        {
            get { return regexCheck.Checked; }
        }

        public FindDialog()
        {
            InitializeComponent();
            queryBox.SelectAll();
            queryBox.Focus();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
