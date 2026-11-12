namespace DevStudio.Dialogs
{
    partial class FindDialog
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label queryLabel;
        private System.Windows.Forms.TextBox queryBox;
        private System.Windows.Forms.CheckBox matchCaseCheck;
        private System.Windows.Forms.CheckBox wholeWordCheck;
        private System.Windows.Forms.CheckBox regexCheck;
        private System.Windows.Forms.Button findButton;
        private System.Windows.Forms.Button cancelButton;

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
            this.queryLabel = new System.Windows.Forms.Label();
            this.queryBox = new System.Windows.Forms.TextBox();
            this.matchCaseCheck = new System.Windows.Forms.CheckBox();
            this.wholeWordCheck = new System.Windows.Forms.CheckBox();
            this.regexCheck = new System.Windows.Forms.CheckBox();
            this.findButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // queryLabel
            //
            this.queryLabel.AutoSize = true;
            this.queryLabel.Location = new System.Drawing.Point(12, 12);
            this.queryLabel.Name = "queryLabel";
            this.queryLabel.Text = "Search in project files:";
            //
            // queryBox
            //
            this.queryBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.queryBox.Location = new System.Drawing.Point(15, 32);
            this.queryBox.Name = "queryBox";
            this.queryBox.Size = new System.Drawing.Size(340, 20);
            this.queryBox.TabIndex = 1;
            //
            // matchCaseCheck
            //
            this.matchCaseCheck.AutoSize = true;
            this.matchCaseCheck.Location = new System.Drawing.Point(15, 62);
            this.matchCaseCheck.Name = "matchCaseCheck";
            this.matchCaseCheck.Text = "Match case";
            this.matchCaseCheck.UseVisualStyleBackColor = true;
            //
            // wholeWordCheck
            //
            this.wholeWordCheck.AutoSize = true;
            this.wholeWordCheck.Location = new System.Drawing.Point(115, 62);
            this.wholeWordCheck.Name = "wholeWordCheck";
            this.wholeWordCheck.Text = "Whole word";
            this.wholeWordCheck.UseVisualStyleBackColor = true;
            //
            // regexCheck
            //
            this.regexCheck.AutoSize = true;
            this.regexCheck.Location = new System.Drawing.Point(225, 62);
            this.regexCheck.Name = "regexCheck";
            this.regexCheck.Text = "Regular expression";
            this.regexCheck.UseVisualStyleBackColor = true;
            //
            // findButton
            //
            this.findButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.findButton.Location = new System.Drawing.Point(199, 92);
            this.findButton.Name = "findButton";
            this.findButton.Size = new System.Drawing.Size(75, 26);
            this.findButton.TabIndex = 5;
            this.findButton.Text = "Find All";
            this.findButton.UseVisualStyleBackColor = true;
            //
            // cancelButton
            //
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.Location = new System.Drawing.Point(280, 92);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 26);
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.TabIndex = 6;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.Click += new System.EventHandler(this.cancelButton_Click);
            //
            // FindDialog
            //
            this.AcceptButton = this.findButton;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(367, 130);
            this.Controls.Add(this.queryLabel);
            this.Controls.Add(this.queryBox);
            this.Controls.Add(this.matchCaseCheck);
            this.Controls.Add(this.wholeWordCheck);
            this.Controls.Add(this.regexCheck);
            this.Controls.Add(this.findButton);
            this.Controls.Add(this.cancelButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FindDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Find";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
