using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DevStudio.Panels
{
    public partial class ErrorListPanel : UserControl
    {
        public event Action<Build.DiagnosticItem> ItemActivated;

        public ErrorListPanel()
        {
            InitializeComponent();
        }

        public void SetDiagnostics(IEnumerable<Build.DiagnosticItem> items)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<IEnumerable<Build.DiagnosticItem>>(SetDiagnostics), items);
                return;
            }
            errorGrid.Rows.Clear();
            foreach (var item in items)
            {
                var index = errorGrid.Rows.Add(
                    item.Severity.ToString(),
                    item.Code,
                    item.Message,
                    item.File,
                    item.Line,
                    item.Column,
                    item.Project);
                errorGrid.Rows[index].Tag = item;
                if (item.Severity == Build.DiagnosticSeverityEx.Error)
                {
                    errorGrid.Rows[index].Cells[0].Style.ForeColor = Color.FromArgb(242, 110, 118);
                }
                else if (item.Severity == Build.DiagnosticSeverityEx.Warning)
                {
                    errorGrid.Rows[index].Cells[0].Style.ForeColor = Color.FromArgb(255, 214, 0);
                }
            }
        }

        public void Clear()
        {
            errorGrid.Rows.Clear();
        }

        private void ErrorGridCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            var item = errorGrid.Rows[e.RowIndex].Tag as Build.DiagnosticItem;
            if (item != null)
            {
                ItemActivated?.Invoke(item);
            }
        }
    }
}
