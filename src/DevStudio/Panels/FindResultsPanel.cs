using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DevStudio.Panels
{
    public partial class FindResultsPanel : UserControl
    {
        public event Action<Services.FindHit> Navigate;

        public FindResultsPanel()
        {
            InitializeComponent();
        }

        public void SetResults(string query, IEnumerable<Services.FindHit> hits)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, IEnumerable<Services.FindHit>>(SetResults), query, hits);
                return;
            }
            resultsList.Items.Clear();
            var group = resultsList.Items.Add("Find \"" + query + "\"");
            group.Font = new System.Drawing.Font(resultsList.Font, System.Drawing.FontStyle.Bold);
            foreach (var hit in hits)
            {
                var item = new ListViewItem(new[]
                {
                    System.IO.Path.GetFileName(hit.File),
                    hit.Line.ToString(),
                    hit.Column.ToString(),
                    hit.LineText
                });
                item.Tag = hit;
                resultsList.Items.Add(item);
            }
        }

        public void Clear()
        {
            resultsList.Items.Clear();
        }

        private void ResultsListDoubleClick(object sender, EventArgs e)
        {
            if (resultsList.SelectedItems.Count == 0)
            {
                return;
            }
            var hit = resultsList.SelectedItems[0].Tag as Services.FindHit;
            if (hit != null)
            {
                Navigate?.Invoke(hit);
            }
        }
    }
}
