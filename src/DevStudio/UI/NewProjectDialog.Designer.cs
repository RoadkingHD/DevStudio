namespace DevStudio.UI
{
    partial class NewProjectDialog
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.Label nameLabel;
        private System.Windows.Forms.TextBox nameText;
        private System.Windows.Forms.Label locationLabel;
        private System.Windows.Forms.TextBox locationText;
        private System.Windows.Forms.Button browseButton;
        private System.Windows.Forms.GroupBox languageGroup;
        private System.Windows.Forms.RadioButton csharpRadio;
        private System.Windows.Forms.RadioButton cppRadio;
        private System.Windows.Forms.Button okButton;
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
            this.titleLabel = new System.Windows.Forms.Label();
            this.nameLabel = new System.Windows.Forms.Label();
            this.nameText = new System.Windows.Forms.TextBox();
            this.locationLabel = new System.Windows.Forms.Label();
            this.locationText = new System.Windows.Forms.TextBox();
            this.browseButton = new System.Windows.Forms.Button();
            this.languageGroup = new System.Windows.Forms.GroupBox();
            this.csharpRadio = new System.Windows.Forms.RadioButton();
            this.cppRadio = new System.Windows.Forms.RadioButton();
            this.okButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.languageGroup.SuspendLayout();
            this.SuspendLayout();
            //
            // titleLabel
            //
            this.titleLabel.AutoSize = true;
            this.titleLabel.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.titleLabel.Location = new System.Drawing.Point(12, 12);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(160, 20);
            this.titleLabel.TabIndex = 0;
            this.titleLabel.Text = "New Project";
            //
            // nameLabel
            //
            this.nameLabel.AutoSize = true;
            this.nameLabel.Location = new System.Drawing.Point(15, 52);
            this.nameLabel.Name = "nameLabel";
            this.nameLabel.Size = new System.Drawing.Size(84, 13);
            this.nameLabel.TabIndex = 1;
            this.nameLabel.Text = "Project &name:";
            //
            // nameText
            //
            this.nameText.Location = new System.Drawing.Point(18, 70);
            this.nameText.Name = "nameText";
            this.nameText.Size = new System.Drawing.Size(360, 20);
            this.nameText.TabIndex = 2;
            //
            // locationLabel
            //
            this.locationLabel.AutoSize = true;
            this.locationLabel.Location = new System.Drawing.Point(15, 102);
            this.locationLabel.Name = "locationLabel";
            this.locationLabel.Size = new System.Drawing.Size(57, 13);
            this.locationLabel.TabIndex = 3;
            this.locationLabel.Text = "&Location:";
            //
            // locationText
            //
            this.locationText.Location = new System.Drawing.Point(18, 120);
            this.locationText.Name = "locationText";
            this.locationText.Size = new System.Drawing.Size(330, 20);
            this.locationText.TabIndex = 4;
            //
            // browseButton
            //
            this.browseButton.Location = new System.Drawing.Point(354, 118);
            this.browseButton.Name = "browseButton";
            this.browseButton.Size = new System.Drawing.Size(24, 23);
            this.browseButton.TabIndex = 5;
            this.browseButton.Text = "...";
            //
            // languageGroup
            //
            this.languageGroup.Controls.Add(this.csharpRadio);
            this.languageGroup.Controls.Add(this.cppRadio);
            this.languageGroup.Location = new System.Drawing.Point(15, 155);
            this.languageGroup.Name = "languageGroup";
            this.languageGroup.Size = new System.Drawing.Size(363, 70);
            this.languageGroup.TabIndex = 6;
            this.languageGroup.TabStop = false;
            this.languageGroup.Text = "Language";
            //
            // csharpRadio
            //
            this.csharpRadio.AutoSize = true;
            this.csharpRadio.Checked = true;
            this.csharpRadio.Location = new System.Drawing.Point(12, 26);
            this.csharpRadio.Name = "csharpRadio";
            this.csharpRadio.Size = new System.Drawing.Size(120, 17);
            this.csharpRadio.TabIndex = 0;
            this.csharpRadio.TabStop = true;
            this.csharpRadio.Text = "C# (Roslyn, debugger)";
            this.csharpRadio.UseVisualStyleBackColor = true;
            //
            // cppRadio
            //
            this.cppRadio.AutoSize = true;
            this.cppRadio.Location = new System.Drawing.Point(12, 46);
            this.cppRadio.Name = "cppRadio";
            this.cppRadio.Size = new System.Drawing.Size(210, 17);
            this.cppRadio.TabIndex = 1;
            this.cppRadio.Text = "C/C++ (external MinGW compiler)";
            this.cppRadio.UseVisualStyleBackColor = true;
            //
            // okButton
            //
            this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.okButton.Location = new System.Drawing.Point(222, 240);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(75, 25);
            this.okButton.TabIndex = 7;
            this.okButton.Text = "OK";
            //
            // cancelButton
            //
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(303, 240);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 25);
            this.cancelButton.TabIndex = 8;
            this.cancelButton.Text = "Cancel";
            //
            // NewProjectDialog
            //
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(393, 281);
            this.Controls.Add(this.titleLabel);
            this.Controls.Add(this.nameLabel);
            this.Controls.Add(this.nameText);
            this.Controls.Add(this.locationLabel);
            this.Controls.Add(this.locationText);
            this.Controls.Add(this.browseButton);
            this.Controls.Add(this.languageGroup);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.cancelButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NewProjectDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "New Project";
            this.languageGroup.ResumeLayout(false);
            this.languageGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
