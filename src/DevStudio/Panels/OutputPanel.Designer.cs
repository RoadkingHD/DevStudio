namespace DevStudio.Panels
{
    partial class OutputPanel
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TextBox outputBox;

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
            this.outputBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // outputBox
            // 
            this.outputBox.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            this.outputBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.outputBox.ForeColor = System.Drawing.Color.FromArgb(212, 212, 212);
            this.outputBox.Location = new System.Drawing.Point(0, 0);
            this.outputBox.Multiline = true;
            this.outputBox.Name = "outputBox";
            this.outputBox.ReadOnly = true;
            this.outputBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.outputBox.Size = new System.Drawing.Size(400, 120);
            this.outputBox.TabIndex = 0;
            this.outputBox.Font = new System.Drawing.Font("Consolas", 9F);
            this.outputBox.WordWrap = false;
            // 
            // OutputPanel
            // 
            this.Controls.Add(this.outputBox);
            this.Name = "OutputPanel";
            this.Size = new System.Drawing.Size(400, 120);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
