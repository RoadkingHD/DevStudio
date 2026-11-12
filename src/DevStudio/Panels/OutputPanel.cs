using System;
using System.Windows.Forms;

namespace DevStudio.Panels
{
    public partial class OutputPanel : UserControl
    {
        public OutputPanel()
        {
            InitializeComponent();
        }

        public void AppendLine(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLine), text);
                return;
            }
            outputBox.AppendText(text + Environment.NewLine);
        }

        public void Clear()
        {
            outputBox.Clear();
        }
    }
}
