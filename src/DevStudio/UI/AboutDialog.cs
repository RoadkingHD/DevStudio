using System.Drawing;
using System.Windows.Forms;
using DevStudio.Theming;

namespace DevStudio.UI
{
    /// <summary>About dialog with a product summary and the full shortcut list.</summary>
    internal sealed class AboutDialog : Form
    {
        public AboutDialog()
        {
            Text = "About DevStudio";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(620, 560);
            BackColor = UiTheme.Window;
            ForeColor = UiTheme.Text;
            Font = new Font("Segoe UI", 9F);

            var header = new Label
            {
                Text = "DevStudio",
                Dock = DockStyle.Top,
                Height = 44,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = UiTheme.Text,
                BackColor = UiTheme.Background2,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };

            var text = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Panel,
                ForeColor = UiTheme.Text,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9F),
                Text = BuildAboutText()
            };

            var ok = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Dock = DockStyle.Bottom,
                Height = 32
            };

            Controls.Add(text);
            Controls.Add(ok);
            Controls.Add(header);
            AcceptButton = ok;
            CancelButton = ok;
        }

        private static string BuildAboutText()
        {
            return
                "DevStudio - a lightweight development studio for C# and C/C++.\r\n" +
                "\r\n" +
                "SUMMARY\r\n" +
                "  C#:        Roslyn editor services (code completion, quick info, semantic\r\n" +
                "             coloring), in-process compiler (Any CPU, x86, x64), ICorDebug\r\n" +
                "             debugger with breakpoints, stepping, call stack and locals,\r\n" +
                "             WinForms designer with toolbox, property grid and code\r\n" +
                "             generation, DLL references.\r\n" +
                "  C/C++:     editor with C/C++ highlighting, external MinGW (g++) compiler,\r\n" +
                "             vcxproj or single-file projects.\r\n" +
                "  Projects:  csproj / vcxproj or single files, solution explorer, color\r\n" +
                "             themes, syntax minimap, find in files.\r\n" +
                "\r\n" +
                "SHORTCUTS\r\n" +
                "  File\r\n" +
                "    Ctrl+N                New File\r\n" +
                "    Ctrl+Shift+N          New Project\r\n" +
                "    Ctrl+O                Open File\r\n" +
                "    Ctrl+Shift+O          Open Project\r\n" +
                "    Ctrl+S                Save\r\n" +
                "    Ctrl+Shift+S          Save All\r\n" +
                "  Build\r\n" +
                "    F6                    Build Solution\r\n" +
                "  Debug\r\n" +
                "    F5                    Start Debugging (saves and rebuilds)\r\n" +
                "    Ctrl+F5               Start Without Debugging (saves and rebuilds)\r\n" +
                "    Shift+F5              Stop Debugging\r\n" +
                "    F9                    Toggle Breakpoint\r\n" +
                "    F10                   Step Over\r\n" +
                "    F11                   Step Into\r\n" +
                "    Shift+F11             Step Out\r\n" +
                "  Editor\r\n" +
                "    Ctrl+F                Find in Files\r\n" +
                "    Ctrl+G                Go To Line\r\n" +
                "    Ctrl+Space            Code Completion\r\n" +
                "    F12                   Go To Definition\r\n" +
                "    Shift+F12             Find References\r\n" +
                "  Designer\r\n" +
                "    Double-click control  Insert default event handler\r\n" +
                "    Del                   Delete selected control\r\n" +
                "    Double-click toolbox  Add control to selected container\r\n" +
                "    Drag from toolbox     Add control at drop position\r\n";
        }
    }
}
