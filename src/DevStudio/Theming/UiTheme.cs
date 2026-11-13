using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DevStudio.Theming
{
    /// <summary>Applies an editor theme to the surrounding WinForms UI.</summary>
    public static class UiTheme
    {
        public static event EventHandler ThemeChanged;

        private static readonly List<TabControl> _closeButtonTabs = new List<TabControl>();

        private static EditorTheme _current = EditorTheme.Dark;
        public static EditorTheme Current
        {
            get { return _current; }
        }

        public static void SetTheme(EditorTheme theme, Control root)
        {
            _current = theme ?? EditorTheme.Dark;
            if (root != null)
            {
                Apply(root);
                root.Refresh();
                var form = root as Form;
                if (form != null)
                {
                    if (form.IsHandleCreated)
                    {
                        ApplyWindowTheme(form);
                    }
                    EventHandler onCreated = null;
                    onCreated = (s, e) =>
                    {
                        form.HandleCreated -= onCreated;
                        ApplyWindowTheme(form);
                    };
                    form.HandleCreated += onCreated;
                }
            }
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static Color Window => Current.IsDark ? Color.FromArgb(37, 37, 38) : Color.FromArgb(240, 240, 240);
        public static Color Panel => Current.IsDark ? Color.FromArgb(45, 45, 48) : Color.White;
        public static Color PanelBorder => Current.IsDark ? Color.FromArgb(63, 63, 70) : Color.FromArgb(204, 204, 204);
        public static Color Text => Current.Foreground;
        public static Color Accent => Current.IsDark ? Color.FromArgb(0, 122, 204) : Color.FromArgb(0, 120, 215);
        public static Color ToolWindow => Current.IsDark ? Color.FromArgb(30, 30, 30) : Color.FromArgb(245, 245, 245);
        public static Color Background2 => Current.IsDark ? Color.FromArgb(45, 45, 48) : Color.FromArgb(245, 245, 245);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_PRE1903 = 19;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public static void ApplyWindowTheme(Form form)
        {
            if (form == null || !form.IsHandleCreated)
            {
                return;
            }
            try
            {
                int dark = Current.IsDark ? 1 : 0;
                DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, 4);
                DwmSetWindowAttribute(form.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_PRE1903, ref dark, 4);
                int caption = ToColorRef(Background2);
                DwmSetWindowAttribute(form.Handle, DWMWA_CAPTION_COLOR, ref caption, 4);
                int textColor = ToColorRef(Text);
                DwmSetWindowAttribute(form.Handle, DWMWA_TEXT_COLOR, ref textColor, 4);
            }
            catch
            {
            }
        }

        private static int ToColorRef(Color color)
        {
            return color.R | (color.G << 8) | (color.B << 16);
        }

        public static void Apply(Control root)
        {
            ApplyRecursive(root);
        }

        private static void ApplyRecursive(Control control)
        {
            if (control is PropertyGrid || control is WebBrowser)
            {
                return;
            }

            if (control is TabControl tab)
            {
                tab.BackColor = Background2;
                tab.ForeColor = Text;
                if (Current.IsDark || _closeButtonTabs.Contains(tab))
                {
                    tab.DrawMode = TabDrawMode.OwnerDrawFixed;
                    tab.DrawItem -= TabOnDrawItem;
                    tab.DrawItem += TabOnDrawItem;
                }
                else
                {
                    tab.DrawMode = TabDrawMode.Normal;
                }
            }
            else if (control is ListView lv)
            {
                lv.BackColor = Panel;
                lv.ForeColor = Text;
            }
            else if (control is DataGridView grid)
            {
                grid.BackgroundColor = Panel;
                grid.GridColor = PanelBorder;
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Panel;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
                grid.DefaultCellStyle.BackColor = Panel;
                grid.DefaultCellStyle.ForeColor = Text;
                grid.DefaultCellStyle.SelectionBackColor = Accent;
                grid.DefaultCellStyle.SelectionForeColor = Color.White;
            }
            else if (control is TreeView tv)
            {
                tv.BackColor = Panel;
                tv.ForeColor = Text;
                tv.DrawMode = TreeViewDrawMode.OwnerDrawText;
                tv.DrawNode -= TreeOnDrawNode;
                tv.DrawNode += TreeOnDrawNode;
            }
            else if (control is TextBox || control is ComboBox || control is NumericUpDown)
            {
                control.BackColor = Current.IsDark ? Color.FromArgb(51, 51, 55) : Color.White;
                control.ForeColor = Text;
            }
            else if (control is Button)
            {
                var button = (Button)control;
                button.BackColor = Current.IsDark ? Color.FromArgb(62, 62, 66) : Color.FromArgb(225, 225, 225);
                button.ForeColor = Text;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = PanelBorder;
            }
            else if (control is MenuStrip || control is ToolStrip || control is StatusStrip)
            {
                var strip = (ToolStrip)control;
                strip.BackColor = Window;
                strip.ForeColor = Text;
                strip.RenderMode = ToolStripRenderMode.Professional;
                strip.Renderer = new ThemeToolStripRenderer(Current.IsDark);
                ApplyToolStripItems(strip.Items);
            }
            else if (control is Panel || control is SplitterPanel || control is GroupBox)
            {
                control.BackColor = Window;
                control.ForeColor = Text;
            }
            else
            {
                control.BackColor = Window;
                control.ForeColor = Text;
            }

            if (control.ContextMenuStrip != null)
            {
                var contextMenu = control.ContextMenuStrip;
                contextMenu.BackColor = Window;
                contextMenu.ForeColor = Text;
                contextMenu.Renderer = new ThemeToolStripRenderer(Current.IsDark);
                ApplyToolStripItems(contextMenu.Items);
            }

            foreach (Control child in control.Controls)
            {
                ApplyRecursive(child);
            }
        }

        private static void ApplyToolStripItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                item.ForeColor = Text;
                var dropDown = item as ToolStripDropDownItem;
                if (dropDown != null)
                {
                    ApplyToolStripItems(dropDown.DropDownItems);
                }
            }
        }

        private static void TreeOnDrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            var tv = (TreeView)sender;
            Color back = e.Node.BackColor == Color.Empty ? tv.BackColor : tv.BackColor;
            Color fore = tv.ForeColor;
            if ((e.State & TreeNodeStates.Selected) != 0)
            {
                back = Accent;
                fore = Color.White;
            }
            e.Graphics.FillRectangle(new SolidBrush(back), e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Node.Text, tv.Font, e.Bounds, fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        public static void EnableTabCloseButtons(TabControl tab)
        {
            if (!_closeButtonTabs.Contains(tab))
            {
                _closeButtonTabs.Add(tab);
            }
            tab.DrawMode = TabDrawMode.OwnerDrawFixed;
            tab.ItemSize = new Size(160, 22);
        }

        public static Rectangle TabCloseRect(Rectangle tabBounds)
        {
            return new Rectangle(tabBounds.Right - 18, tabBounds.Top + 5, 13, tabBounds.Height - 10);
        }

        private static void TabOnDrawItem(object sender, DrawItemEventArgs e)
        {
            var tab = (TabControl)sender;
            var page = tab.TabPages[e.Index];
            var bounds = tab.GetTabRect(e.Index);
            bool selected = tab.SelectedIndex == e.Index;
            using (var back = new SolidBrush(selected ? Accent : Window))
            {
                e.Graphics.FillRectangle(back, bounds);
            }
            var textBounds = _closeButtonTabs.Contains(tab)
                ? new Rectangle(bounds.X + 6, bounds.Y, bounds.Width - 26, bounds.Height)
                : bounds;
            TextRenderer.DrawText(e.Graphics, page.Text, tab.Font, textBounds, selected ? Color.White : Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            if (_closeButtonTabs.Contains(tab))
            {
                var closeRect = TabCloseRect(bounds);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(selected ? Color.White : Text, 1.4f))
                {
                    e.Graphics.DrawLine(pen, closeRect.Left + 3, closeRect.Top + 3, closeRect.Right - 3, closeRect.Bottom - 3);
                    e.Graphics.DrawLine(pen, closeRect.Right - 3, closeRect.Top + 3, closeRect.Left + 3, closeRect.Bottom - 3);
                }
            }
        }

        private sealed class ThemeToolStripRenderer : ToolStripProfessionalRenderer
        {
            private readonly bool _dark;

            public ThemeToolStripRenderer(bool dark)
                : base(new ThemeColorTable(dark))
            {
                _dark = dark;
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var rect = new Rectangle(Point.Empty, e.Item.Size);
                if (e.Item.Selected)
                {
                    using (var b = new SolidBrush(Accent))
                    {
                        e.Graphics.FillRectangle(b, rect);
                    }
                }
                else
                {
                    using (var b = new SolidBrush(_dark ? Color.FromArgb(37, 37, 38) : Color.FromArgb(240, 240, 240)))
                    {
                        e.Graphics.FillRectangle(b, rect);
                    }
                }
            }
        }

        private sealed class ThemeColorTable : ProfessionalColorTable
        {
            private readonly bool _dark;

            public ThemeColorTable(bool dark)
            {
                _dark = dark;
                UseSystemColors = false;
            }

            public override Color MenuStripGradientBegin => _dark ? Color.FromArgb(37, 37, 38) : Color.FromArgb(240, 240, 240);
            public override Color MenuStripGradientEnd => _dark ? Color.FromArgb(37, 37, 38) : Color.FromArgb(240, 240, 240);
            public override Color ToolStripDropDownBackground => _dark ? Color.FromArgb(45, 45, 48) : Color.White;
            public override Color ImageMarginGradientBegin => _dark ? Color.FromArgb(45, 45, 48) : Color.White;
            public override Color ImageMarginGradientMiddle => _dark ? Color.FromArgb(45, 45, 48) : Color.White;
            public override Color ImageMarginGradientEnd => _dark ? Color.FromArgb(45, 45, 48) : Color.White;
            public override Color MenuBorder => _dark ? Color.FromArgb(63, 63, 70) : Color.FromArgb(204, 204, 204);
            public override Color MenuItemBorder => Color.Transparent;
        }
    }
}
