using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DevStudio.Panels
{
    public partial class BreakpointsPanel : UserControl
    {
        public event Action<string, int> Navigate;
        public event Action<string, int> RemoveRequested;

        public BreakpointsPanel()
        {
            InitializeComponent();
            var menu = new ContextMenuStrip();
            var remove = new ToolStripMenuItem("Delete");
            remove.Click += (s, e) =>
            {
                if (bpList.SelectedItems.Count > 0)
                {
                    var bp = bpList.SelectedItems[0].Tag as BreakpointEntry;
                    if (bp != null)
                    {
                        RemoveRequested?.Invoke(bp.File, bp.Line);
                    }
                }
            };
            menu.Items.Add(remove);
            bpList.ContextMenuStrip = menu;
        }

        public sealed class BreakpointEntry
        {
            public string File { get; set; }
            public int Line { get; set; }
        }

        public void SetBreakpoints(IEnumerable<BreakpointEntry> breakpoints)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<IEnumerable<BreakpointEntry>>(SetBreakpoints), breakpoints);
                return;
            }
            var selected = bpList.SelectedItems.Count > 0 ? bpList.SelectedItems[0].Tag : null;
            bpList.Items.Clear();
            foreach (var bp in breakpoints)
            {
                var item = new ListViewItem(new[]
                {
                    System.IO.Path.GetFileName(bp.File),
                    bp.Line.ToString()
                });
                item.Tag = bp;
                bpList.Items.Add(item);
                if (selected == bp)
                {
                    item.Selected = true;
                }
            }
        }

        public void Clear()
        {
            bpList.Items.Clear();
        }

        private void BpListDoubleClick(object sender, EventArgs e)
        {
            if (bpList.SelectedItems.Count == 0)
            {
                return;
            }
            var bp = bpList.SelectedItems[0].Tag as BreakpointEntry;
            if (bp != null)
            {
                Navigate?.Invoke(bp.File, bp.Line);
            }
        }
    }
}
