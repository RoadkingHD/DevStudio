namespace DevStudio.Panels
{
    partial class CallStackPanel
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ListView stackList;
        private System.Windows.Forms.ColumnHeader methodHeader;
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
            this.stackList = new System.Windows.Forms.ListView();
            this.methodHeader = new System.Windows.Forms.ColumnHeader();
            this.fileHeader = new System.Windows.Forms.ColumnHeader();
            this.lineHeader = new System.Windows.Forms.ColumnHeader();
            this.SuspendLayout();
            //
            // stackList
            //
            this.stackList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.methodHeader,
            this.fileHeader,
            this.lineHeader});
            this.stackList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.stackList.FullRowSelect = true;
            this.stackList.Location = new System.Drawing.Point(0, 0);
            this.stackList.Name = "stackList";
            this.stackList.Size = new System.Drawing.Size(500, 150);
            this.stackList.TabIndex = 0;
            this.stackList.UseCompatibleStateImageBehavior = false;
            this.stackList.View = System.Windows.Forms.View.Details;
            this.stackList.DoubleClick += new System.EventHandler(this.StackListDoubleClick);
            //
            // headers
            //
            this.methodHeader.Text = "Method";
            this.methodHeader.Width = 320;
            this.fileHeader.Text = "File";
            this.fileHeader.Width = 150;
            this.lineHeader.Text = "Line";
            this.lineHeader.Width = 60;
            //
            // CallStackPanel
            //
            this.Controls.Add(this.stackList);
            this.Name = "CallStackPanel";
            this.Size = new System.Drawing.Size(500, 150);
            this.ResumeLayout(false);
        }
    }
}
