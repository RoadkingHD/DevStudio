namespace DevStudio.Panels
{
    partial class LocalsPanel
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ListView localsList;
        private System.Windows.Forms.ColumnHeader nameHeader;
        private System.Windows.Forms.ColumnHeader typeHeader;
        private System.Windows.Forms.ColumnHeader valueHeader;

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
            this.localsList = new System.Windows.Forms.ListView();
            this.nameHeader = new System.Windows.Forms.ColumnHeader();
            this.typeHeader = new System.Windows.Forms.ColumnHeader();
            this.valueHeader = new System.Windows.Forms.ColumnHeader();
            this.SuspendLayout();
            //
            // localsList
            //
            this.localsList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.nameHeader,
            this.typeHeader,
            this.valueHeader});
            this.localsList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.localsList.FullRowSelect = true;
            this.localsList.Location = new System.Drawing.Point(0, 0);
            this.localsList.Name = "localsList";
            this.localsList.Size = new System.Drawing.Size(300, 150);
            this.localsList.TabIndex = 0;
            this.localsList.UseCompatibleStateImageBehavior = false;
            this.localsList.View = System.Windows.Forms.View.Details;
            //
            // headers
            //
            this.nameHeader.Text = "Name";
            this.nameHeader.Width = 120;
            this.typeHeader.Text = "Type";
            this.typeHeader.Width = 100;
            this.valueHeader.Text = "Value";
            this.valueHeader.Width = 160;
            //
            // LocalsPanel
            //
            this.Controls.Add(this.localsList);
            this.Name = "LocalsPanel";
            this.Size = new System.Drawing.Size(300, 150);
            this.ResumeLayout(false);
        }
    }
}
