namespace DevStudio.Panels
{
    partial class SolutionExplorerPanel
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TreeView projectTree;

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
            this.projectTree = new System.Windows.Forms.TreeView();
            this.SuspendLayout();
            //
            // projectTree
            //
            this.projectTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.projectTree.HideSelection = false;
            this.projectTree.Location = new System.Drawing.Point(0, 0);
            this.projectTree.Name = "projectTree";
            this.projectTree.Size = new System.Drawing.Size(250, 400);
            this.projectTree.TabIndex = 0;
            this.projectTree.DoubleClick += new System.EventHandler(this.ProjectTreeDoubleClick);
            //
            // SolutionExplorerPanel
            //
            this.Controls.Add(this.projectTree);
            this.Name = "SolutionExplorerPanel";
            this.Size = new System.Drawing.Size(250, 400);
            this.ResumeLayout(false);
        }
    }
}
