namespace DevStudio.Panels
{
    partial class FindResultsPanel
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ListView resultsList;
        private System.Windows.Forms.ColumnHeader fileHeader;
        private System.Windows.Forms.ColumnHeader lineHeader;
        private System.Windows.Forms.ColumnHeader columnHeader;
        private System.Windows.Forms.ColumnHeader textHeader;

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
            this.resultsList = new System.Windows.Forms.ListView();
            this.fileHeader = new System.Windows.Forms.ColumnHeader();
            this.lineHeader = new System.Windows.Forms.ColumnHeader();
            this.columnHeader = new System.Windows.Forms.ColumnHeader();
            this.textHeader = new System.Windows.Forms.ColumnHeader();
            this.SuspendLayout();
            //
            // resultsList
            //
            this.resultsList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.fileHeader,
            this.lineHeader,
            this.columnHeader,
            this.textHeader});
            this.resultsList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.resultsList.FullRowSelect = true;
            this.resultsList.Location = new System.Drawing.Point(0, 0);
            this.resultsList.Name = "resultsList";
            this.resultsList.Size = new System.Drawing.Size(500, 150);
            this.resultsList.TabIndex = 0;
            this.resultsList.UseCompatibleStateImageBehavior = false;
            this.resultsList.View = System.Windows.Forms.View.Details;
            this.resultsList.DoubleClick += new System.EventHandler(this.ResultsListDoubleClick);
            //
            // headers
            //
            this.fileHeader.Text = "File";
            this.fileHeader.Width = 120;
            this.lineHeader.Text = "Line";
            this.lineHeader.Width = 50;
            this.columnHeader.Text = "Column";
            this.columnHeader.Width = 60;
            this.textHeader.Text = "Text";
            this.textHeader.Width = 300;
            //
            // FindResultsPanel
            //
            this.Controls.Add(this.resultsList);
            this.Name = "FindResultsPanel";
            this.Size = new System.Drawing.Size(500, 150);
            this.ResumeLayout(false);
        }
    }
}
