namespace DevStudio.Panels
{
    partial class ErrorListPanel
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.DataGridView errorGrid;

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
            this.errorGrid = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.errorGrid)).BeginInit();
            this.SuspendLayout();
            //
            // errorGrid
            //
            this.errorGrid.AllowUserToAddRows = false;
            this.errorGrid.AllowUserToDeleteRows = false;
            this.errorGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.errorGrid.BackgroundColor = System.Drawing.Color.FromArgb(37, 37, 38);
            this.errorGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.errorGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            new System.Windows.Forms.DataGridViewTextBoxColumn { Name = "Severity", HeaderText = "Severity", FillWeight = 12F, ReadOnly = true },
            new System.Windows.Forms.DataGridViewTextBoxColumn { Name = "Code", HeaderText = "Code", FillWeight = 10F, ReadOnly = true },
            new System.Windows.Forms.DataGridViewTextBoxColumn { Name = "Message", HeaderText = "Message", FillWeight = 45F, ReadOnly = true },
            new System.Windows.Forms.DataGridViewTextBoxColumn { Name = "File", HeaderText = "File", FillWeight = 18F, ReadOnly = true },
            new System.Windows.Forms.DataGridViewTextBoxColumn { Name = "Line", HeaderText = "Line", FillWeight = 7F, ReadOnly = true },
            new System.Windows.Forms.DataGridViewTextBoxColumn { Name = "Column", HeaderText = "Column", FillWeight = 8F, ReadOnly = true },
            new System.Windows.Forms.DataGridViewTextBoxColumn { Name = "Project", HeaderText = "Project", FillWeight = 12F, ReadOnly = true }});
            this.errorGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.errorGrid.Location = new System.Drawing.Point(0, 0);
            this.errorGrid.Name = "errorGrid";
            this.errorGrid.RowHeadersVisible = false;
            this.errorGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.errorGrid.Size = new System.Drawing.Size(500, 150);
            this.errorGrid.TabIndex = 0;
            this.errorGrid.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.ErrorGridCellDoubleClick);
            //
            // ErrorListPanel
            //
            this.Controls.Add(this.errorGrid);
            this.Name = "ErrorListPanel";
            this.Size = new System.Drawing.Size(500, 150);
            ((System.ComponentModel.ISupportInitialize)(this.errorGrid)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
