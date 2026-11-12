using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DevStudio.Panels
{
    public partial class CallStackPanel : UserControl
    {
        public event Action<Debugger.DebugStackFrame> Navigate;

        public CallStackPanel()
        {
            InitializeComponent();
        }

        public void SetCallStack(IEnumerable<Debugger.DebugStackFrame> frames)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<IEnumerable<Debugger.DebugStackFrame>>(SetCallStack), frames);
                return;
            }
            stackList.Items.Clear();
            foreach (var frame in frames)
            {
                var item = new ListViewItem(new[]
                {
                    frame.Method,
                    string.IsNullOrEmpty(frame.File) ? "" : System.IO.Path.GetFileName(frame.File),
                    frame.Line > 0 ? frame.Line.ToString() : ""
                });
                item.Tag = frame;
                stackList.Items.Add(item);
            }
        }

        public void Clear()
        {
            stackList.Items.Clear();
        }

        private void StackListDoubleClick(object sender, EventArgs e)
        {
            if (stackList.SelectedItems.Count == 0)
            {
                return;
            }
            var frame = stackList.SelectedItems[0].Tag as Debugger.DebugStackFrame;
            if (frame != null)
            {
                Navigate?.Invoke(frame);
            }
        }
    }
}
