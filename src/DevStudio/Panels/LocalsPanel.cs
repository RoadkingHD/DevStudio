using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DevStudio.Panels
{
    public partial class LocalsPanel : UserControl
    {
        public LocalsPanel()
        {
            InitializeComponent();
        }

        public void SetLocals(IEnumerable<Debugger.DebugVariable> locals)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<IEnumerable<Debugger.DebugVariable>>(SetLocals), locals);
                return;
            }
            localsList.Items.Clear();
            foreach (var local in locals)
            {
                var item = new ListViewItem(new[]
                {
                    local.Name,
                    local.TypeName,
                    local.Value
                });
                localsList.Items.Add(item);
            }
        }

        public void Clear()
        {
            localsList.Items.Clear();
        }
    }
}
