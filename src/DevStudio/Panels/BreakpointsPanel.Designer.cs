namespace DevStudio.Panels
{
    partial class BreakpointsPanel
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ListView bpList;
        private System.Windows.Forms.ColumnHeader fileHeader;
        private System.Windows.Forms.ColumnHeader lineHeader;

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
            this.bpList = new System.Windows.Forms.ListView();
            this.fileHeader = new System.Windows.Forms.ColumnHeader();
            this.lineHeader = new System.Windows.Forms.ColumnHeader();
            this.SuspendLayout();
            //
            // bpList
            //
            this.bpList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.fileHeader,
            this.lineHeader});
            this.bpList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.bpList.FullRowSelect = true;
            this.bpList.Location = new System.Drawing.Point(0, 0);
            this.bpList.Name = "bpList";
            this.bpList.Size = new System.Drawing.Size(300, 150);
            this.bpList.TabIndex = 0;
            this.bpList.UseCompatibleStateImageBehavior = false;
            this.bpList.View = System.Windows.Forms.View.Details;
            this.bpList.DoubleClick += new System.EventHandler(this.BpListDoubleClick);
            //
            // headers
            //
            this.fileHeader.Text = "File";
            this.fileHeader.Width = 220;
            this.lineHeader.Text = "Line";
            this.lineHeader.Width = 60;
            //
            // BreakpointsPanel
            //
            this.Controls.Add(this.bpList);
            this.Name = "BreakpointsPanel";
            this.Size = new System.Drawing.Size(300, 150);
            this.ResumeLayout(false);
        }
    }
}
