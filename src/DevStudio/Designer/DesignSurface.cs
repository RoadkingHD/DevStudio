using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace DevStudio.Designer
{
    /// <summary>Design surface that instantiates the parsed controls, supports selection,
    /// dragging and property editing, and pushes changes back to the model.</summary>
    public sealed class DesignSurface : Panel
    {
        private enum ResizeHandle
        {
            None,
            N,
            S,
            E,
            W,
            NW,
            NE,
            SW,
            SE
        }

        private const int HandleSize = 8;
        private const int GrabMargin = 5;

        private DesignerModel _model;
        private Control _selected;
        private Control _rootControl;
        private bool _dragging;
        private Point _dragStart;
        private Rectangle _dragStartBounds;
        private ResizeHandle _resizing = ResizeHandle.None;

        public event Action ModelChanged;
        public event Action<Control> SelectionChanged;
        public event Action<string> Diagnostic;
        public event Action<Control> DefaultEventRequested;
        public event Action<string, Point> ToolboxDropRequested;

        public DesignSurface()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(150, 150, 155);
            AllowDrop = true;
            MouseDown += (s, e) =>
            {
                if (_rootControl != null)
                {
                    Select(_rootControl);
                }
            };
            DragEnter += (s, e) =>
            {
                if (e.Data != null && e.Data.GetDataPresent(DataFormats.Text))
                {
                    e.Effect = DragDropEffects.Copy;
                }
            };
            DragDrop += (s, e) =>
            {
                var text = e.Data.GetData(DataFormats.Text) as string;
                if (text == null)
                {
                    return;
                }
                ToolboxDropRequested?.Invoke(text, PointToClient(new Point(e.X, e.Y)));
            };
        }

        public Control SelectedControl
        {
            get { return _selected; }
        }

        public Control RootControl
        {
            get { return _rootControl; }
        }

        public DesignerModel Model
        {
            get { return _model; }
        }

        public void LoadModel(DesignerModel model)
        {
            SuspendLayout();
            Controls.Clear();
            _selected = null;
            _model = model;

            if (model == null || !model.Valid)
            {
                ResumeLayout();
                Invalidate();
                return;
            }

            try
            {
                LoadModelCore(model);
            }
            catch
            {
                Controls.Clear();
                _rootControl = null;
            }
            finally
            {
                ResumeLayout();
                Invalidate();
            }
        }

        private void LoadModelCore(DesignerModel model)
        {
            int width = 640, height = 480;
            bool sizeIsClient = false;
            var clientSize = model.Root.Properties["ClientSize"];
            if (clientSize != null)
            {
                var size = (Size?)DesignerExpressionEvaluator.Evaluate(clientSize, typeof(Size));
                if (size.HasValue)
                {
                    width = size.Value.Width;
                    height = size.Value.Height;
                    sizeIsClient = true;
                }
            }
            if (!sizeIsClient)
            {
                var sizeProp = model.Root.Properties["Size"];
                if (sizeProp != null)
                {
                    var size = (Size?)DesignerExpressionEvaluator.Evaluate(sizeProp, typeof(Size));
                    if (size.HasValue)
                    {
                        width = size.Value.Width;
                        height = size.Value.Height;
                    }
                }
            }

            _rootControl = CreateControlFor(model.Root);
            if (_rootControl == null)
            {
                _rootControl = CreatePlaceholder(width, height);
            }
            if (sizeIsClient)
            {
                _rootControl.ClientSize = new Size(width, height);
            }
            else
            {
                _rootControl.Size = new Size(width, height);
            }
            _rootControl.Location = new Point(0, 0);
            _rootControl.Tag = "this";
            WireControl(_rootControl);
            Paint -= OnSurfacePaint;
            Paint += OnSurfacePaint;
            Location = new Point(8, 8);
            Size = new Size(_rootControl.Width + 16, _rootControl.Height + 16);

            foreach (var control in model.Controls.Values.Where(c => !c.IsRoot))
            {
                var instance = CreateControlFor(control);
                if (instance == null)
                {
                    continue;
                }
                instance.Tag = control.Name;
                WireControl(instance);
                var parent = FindParentControl(control) ?? _rootControl;
                try
                {
                    parent.Controls.Add(instance);
                }
                catch
                {
                }
            }

            Controls.Add(_rootControl);
            _rootControl.BringToFront();
            Select(_rootControl);
        }

        private Control FindParentControl(ControlInfo info)
        {
            if (_rootControl == null)
            {
                return null;
            }
            foreach (var parent in _model.Controls.Values)
            {
                if (parent.Children.Contains(info.Name))
                {
                    if (parent.IsRoot)
                    {
                        return _rootControl;
                    }
                    return FindByName(_rootControl, parent.Name);
                }
            }
            return null;
        }

        private Control FindByName(Control root, string name)
        {
            foreach (Control child in root.Controls)
            {
                if (string.Equals((string)child.Tag, name, StringComparison.Ordinal))
                {
                    return child;
                }
                var nested = FindByName(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }
            return null;
        }

        private Control CreateControlFor(ControlInfo info)
        {
            try
            {
                var type = Type.GetType(info.TypeName + ", System.Windows.Forms");
                if (type == null)
                {
                    type = typeof(Form).Assembly.GetType(info.TypeName);
                }
                if (type == null)
                {
                    type = Type.GetType(info.TypeName);
                }
                if (type == null)
                {
                    RaiseDiagnostic("Designer: type not found for '" + info.Name + "': " + info.TypeName);
                    return CreatePlaceholder(200, 100);
                }
                if (!typeof(Control).IsAssignableFrom(type))
                {
                    return null;
                }
                var instance = (Control)Activator.CreateInstance(type);
                var form = instance as Form;
                instance.Name = info.IsRoot ? "" : info.Name;

                foreach (var property in info.Properties.Entries)
                {
                    ApplyProperty(instance, property.Key, property.Value);
                }
                ApplyItems(instance, info);
                if (form != null)
                {
                    form.TopLevel = false;
                    form.ControlBox = false;
                    form.WindowState = FormWindowState.Normal;
                    form.StartPosition = FormStartPosition.Manual;
                    if (!form.Visible)
                    {
                        form.Visible = true;
                    }
                }
                return instance;
            }
            catch (Exception ex)
            {
                RaiseDiagnostic("Designer: could not create '" + info.Name + "' (" + info.TypeName + "): " + ex.Message);
                return CreatePlaceholder(200, 100);
            }
        }

        private void RaiseDiagnostic(string message)
        {
            Diagnostic?.Invoke(message);
        }

        private static Control CreatePlaceholder(int width, int height)
        {
            return new Panel
            {
                Size = new Size(width, height),
                BackColor = Color.FromArgb(200, 200, 205),
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private static readonly HashSet<string> RootDesignBlocked = new HashSet<string>(StringComparer.Ordinal)
        {
            "TopLevel", "Visible", "Location", "WindowState"
        };

        public bool IsDesignBlocked(Control control, string propertyName)
        {
            return ReferenceEquals(control, _rootControl) && RootDesignBlocked.Contains(propertyName);
        }

        private static void ApplyItems(Control instance, ControlInfo info)
        {
            if (info.Items.Count == 0)
            {
                return;
            }
            try
            {
                var itemsProperty = instance.GetType().GetProperty("Items");
                if (itemsProperty == null)
                {
                    return;
                }
                var collection = itemsProperty.GetValue(instance, null);
                if (collection == null)
                {
                    return;
                }
                var addRange = collection.GetType().GetMethod("AddRange", new[] { typeof(object[]) });
                if (addRange == null)
                {
                    return;
                }
                var values = new object[info.Items.Count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = DesignerExpressionEvaluator.Evaluate(info.Items[i], typeof(object));
                }
                addRange.Invoke(collection, new object[] { values });
            }
            catch
            {
            }
        }

        public void UpdateModelItems(Control control, string[] items)
        {
            if (_model == null || control == null)
            {
                return;
            }
            var name = (string)control.Tag ?? "this";
            ControlInfo info;
            if (!_model.Controls.TryGetValue(name, out info))
            {
                return;
            }
            info.Items.Clear();
            foreach (var item in items)
            {
                info.Items.Add("\"" + item.Replace("\"", "\\\"") + "\"");
            }
            try
            {
                var itemsProperty = control.GetType().GetProperty("Items");
                var collection = itemsProperty != null ? itemsProperty.GetValue(control, null) : null;
                var addRange = collection != null ? collection.GetType().GetMethod("AddRange", new[] { typeof(object[]) }) : null;
                if (collection != null && addRange != null)
                {
                    var clear = collection.GetType().GetMethod("Clear", Type.EmptyTypes);
                    if (clear != null)
                    {
                        clear.Invoke(collection, null);
                    }
                    addRange.Invoke(collection, new object[] { items.Cast<object>().ToArray() });
                }
            }
            catch
            {
            }
            ModelChanged?.Invoke();
        }

        private static void ApplyProperty(Control instance, string propertyName, string expression)
        {
            try
            {
                var property = instance.GetType().GetProperty(propertyName);
                if (property == null || !property.CanWrite)
                {
                    return;
                }
                var value = DesignerExpressionEvaluator.Evaluate(expression, property.PropertyType);
                if (value != null)
                {
                    property.SetValue(instance, value, null);
                }
            }
            catch
            {
            }
        }

        private void WireControl(Control control)
        {
            var hook = new ControlMouseHook(this, control);
            control.HandleCreated += (s, e) => hook.Attach();
            control.HandleDestroyed += (s, e) => hook.ReleaseHandle();
            hook.Attach();
            control.Paint += OnControlPaint;
        }

        internal void HandleHookMouseDown(Control control, Point p, int clicks)
        {
            Select(control);
            Focus();
            if (clicks >= 2)
            {
                DefaultEventRequested?.Invoke(control);
                return;
            }
            var handle = GetHandleAt(GetDesignRect(control), p);
            if (handle != ResizeHandle.None)
            {
                _resizing = handle;
                _dragging = false;
                _dragStart = control.PointToScreen(p);
                _dragStartBounds = control.Bounds;
                control.Capture = true;
                return;
            }
            if (ReferenceEquals(control, _rootControl))
            {
                return;
            }
            _dragging = true;
            _dragStart = control.PointToScreen(p);
            _dragStartBounds = control.Bounds;
            control.Capture = true;
        }

        internal void HandleHookMouseMove(Control control, Point p)
        {
            if (_resizing != ResizeHandle.None && _selected != null)
            {
                var screen = control.PointToScreen(p);
                var delta = new Point(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
                ApplyResize(delta);
                EnsureSurfaceFits();
                return;
            }
            if (_dragging && _selected != null)
            {
                var screen = control.PointToScreen(p);
                var delta = new Point(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
                _selected.Bounds = new Rectangle(
                    Math.Max(0, _dragStartBounds.X + delta.X),
                    Math.Max(0, _dragStartBounds.Y + delta.Y),
                    _dragStartBounds.Width,
                    _dragStartBounds.Height);
                EnsureSurfaceFits();
            }
        }

        internal void HandleHookMouseUp(Control control, Point p)
        {
            if (_resizing != ResizeHandle.None && _selected != null)
            {
                _resizing = ResizeHandle.None;
                control.Capture = false;
                UpdateModelFromControl(_selected);
                if (ReferenceEquals(_selected, _rootControl))
                {
                    SyncSurfaceToRoot();
                }
                SelectionChanged?.Invoke(_selected);
                return;
            }
            if (_dragging && _selected != null)
            {
                _dragging = false;
                control.Capture = false;
                UpdateModelFromControl(_selected);
                SelectionChanged?.Invoke(_selected);
            }
        }

        private void SyncSurfaceToRoot()
        {
            if (_rootControl == null)
            {
                return;
            }
            Size = new Size(_rootControl.Width + 16, _rootControl.Height + 16);
        }

        internal void HandleHookSetCursor(Control control)
        {
            var p = control.PointToClient(Cursor.Position);
            var cursor = GetCursor(GetHandleAt(GetDesignRect(control), p));
            if (cursor == null)
            {
                cursor = ReferenceEquals(control, _rootControl) ? Cursors.Default : Cursors.SizeAll;
            }
            Cursor.Current = cursor;
        }

        private void EnsureSurfaceFits()
        {
            if (_rootControl == null)
            {
                return;
            }
            int right = 0;
            int bottom = 0;
            foreach (Control control in Controls)
            {
                right = Math.Max(right, control.Right);
                bottom = Math.Max(bottom, control.Bottom);
            }
            right += 8;
            bottom += 8;
            if (Width < right || Height < bottom)
            {
                Size = new Size(Math.Max(Width, right), Math.Max(Height, bottom));
            }
        }

        public void SelectNamed(string name)
        {
            var control = FindByName(_rootControl, name);
            if (control != null)
            {
                Select(control);
            }
        }

        private sealed class ControlMouseHook : NativeWindow
        {
            private const int WM_SETCURSOR = 0x0020;
            private const int WM_NCHITTEST = 0x0084;
            private const int WM_MOUSEMOVE = 0x0200;
            private const int WM_LBUTTONDOWN = 0x0201;
            private const int WM_LBUTTONUP = 0x0202;
            private const int WM_LBUTTONDBLCLK = 0x0203;

            private readonly DesignSurface _surface;
            private readonly Control _control;

            public ControlMouseHook(DesignSurface surface, Control control)
            {
                _surface = surface;
                _control = control;
            }

            public void Attach()
            {
                if (_control.IsHandleCreated && Handle == IntPtr.Zero)
                {
                    AssignHandle(_control.Handle);
                }
            }

            protected override void WndProc(ref Message m)
            {
                switch (m.Msg)
                {
                    case WM_NCHITTEST:
                        m.Result = (IntPtr)1;
                        return;
                    case WM_LBUTTONDOWN:
                        _surface.HandleHookMouseDown(_control, PointFromLParam(m.LParam), 1);
                        m.Result = IntPtr.Zero;
                        return;
                    case WM_LBUTTONDBLCLK:
                        _surface.HandleHookMouseDown(_control, PointFromLParam(m.LParam), 2);
                        m.Result = IntPtr.Zero;
                        return;
                    case WM_MOUSEMOVE:
                        _surface.HandleHookMouseMove(_control, PointFromLParam(m.LParam));
                        m.Result = IntPtr.Zero;
                        return;
                    case WM_LBUTTONUP:
                        _surface.HandleHookMouseUp(_control, PointFromLParam(m.LParam));
                        m.Result = IntPtr.Zero;
                        return;
                    case WM_SETCURSOR:
                        if (((int)m.LParam.ToInt64() & 0xFFFF) == 1)
                        {
                            _surface.HandleHookSetCursor(_control);
                            m.Result = (IntPtr)1;
                            return;
                        }
                        base.WndProc(ref m);
                        return;
                }
                base.WndProc(ref m);
            }

            private static Point PointFromLParam(IntPtr lparam)
            {
                long value = lparam.ToInt64() & 0xFFFFFFFF;
                return new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
            }
        }

        private static ResizeHandle GetHandleAt(Rectangle bounds, Point p)
        {
            bool left = Math.Abs(p.X - bounds.Left) <= GrabMargin;
            bool right = Math.Abs(p.X - bounds.Right) <= GrabMargin;
            bool top = Math.Abs(p.Y - bounds.Top) <= GrabMargin;
            bool bottom = Math.Abs(p.Y - bounds.Bottom) <= GrabMargin;
            bool middleX = p.X > bounds.Left + GrabMargin && p.X < bounds.Right - GrabMargin;
            bool middleY = p.Y > bounds.Top + GrabMargin && p.Y < bounds.Bottom - GrabMargin;
            if (left && top)
            {
                return ResizeHandle.NW;
            }
            if (right && top)
            {
                return ResizeHandle.NE;
            }
            if (left && bottom)
            {
                return ResizeHandle.SW;
            }
            if (right && bottom)
            {
                return ResizeHandle.SE;
            }
            if (left && middleY)
            {
                return ResizeHandle.W;
            }
            if (right && middleY)
            {
                return ResizeHandle.E;
            }
            if (top && middleX)
            {
                return ResizeHandle.N;
            }
            if (bottom && middleX)
            {
                return ResizeHandle.S;
            }
            return ResizeHandle.None;
        }

        private static Cursor GetCursor(ResizeHandle handle)
        {
            switch (handle)
            {
                case ResizeHandle.N:
                case ResizeHandle.S:
                    return Cursors.SizeNS;
                case ResizeHandle.E:
                case ResizeHandle.W:
                    return Cursors.SizeWE;
                case ResizeHandle.NW:
                case ResizeHandle.SE:
                    return Cursors.SizeNWSE;
                case ResizeHandle.NE:
                case ResizeHandle.SW:
                    return Cursors.SizeNESW;
                default:
                    return null;
            }
        }

        private void ApplyResize(Point delta)
        {
            var bounds = _dragStartBounds;
            int left = bounds.Left;
            int top = bounds.Top;
            int right = bounds.Right;
            int bottom = bounds.Bottom;
            switch (_resizing)
            {
                case ResizeHandle.NW:
                case ResizeHandle.W:
                case ResizeHandle.SW:
                    left = Math.Max(0, bounds.Left + delta.X);
                    break;
                case ResizeHandle.NE:
                case ResizeHandle.E:
                case ResizeHandle.SE:
                    right = Math.Max(left + 10, bounds.Right + delta.X);
                    break;
            }
            switch (_resizing)
            {
                case ResizeHandle.NW:
                case ResizeHandle.N:
                case ResizeHandle.NE:
                    top = Math.Max(0, bounds.Top + delta.Y);
                    break;
                case ResizeHandle.SW:
                case ResizeHandle.S:
                case ResizeHandle.SE:
                    bottom = Math.Max(top + 10, bounds.Bottom + delta.Y);
                    break;
            }
            _selected.Bounds = new Rectangle(left, top, right - left, bottom - top);
        }

        private static Rectangle GetDesignRect(Control control)
        {
            var parent = control.Parent;
            if (parent != null)
            {
                var origin = control.PointToClient(parent.PointToScreen(control.Bounds.Location));
                return new Rectangle(origin, control.Size);
            }
            return control.ClientRectangle;
        }

        private void OnControlPaint(object sender, PaintEventArgs e)
        {
            var control = (Control)sender;
            if (ReferenceEquals(control, _rootControl))
            {
                return;
            }
            if (ReferenceEquals(control, _selected))
            {
                var designRect = GetDesignRect(control);
                using (var pen = new Pen(Color.FromArgb(0, 122, 204), 1.5f))
                {
                    e.Graphics.DrawRectangle(pen, designRect.Left, designRect.Top, designRect.Width - 1, designRect.Height - 1);
                }
                DrawHandleRects(e.Graphics, designRect);
            }
        }

        private void OnSurfacePaint(object sender, PaintEventArgs e)
        {
            if (_rootControl == null || !ReferenceEquals(_selected, _rootControl))
            {
                return;
            }
            var bounds = _rootControl.Bounds;
            using (var pen = new Pen(Color.FromArgb(0, 122, 204), 1.5f))
            {
                e.Graphics.DrawRectangle(pen, bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1);
            }
            DrawHandleRects(e.Graphics, bounds);
        }

        private void DrawHandleRects(Graphics graphics, Rectangle bounds)
        {
            foreach (var rect in GetHandleRects(bounds))
            {
                using (var brush = new SolidBrush(Color.White))
                {
                    graphics.FillRectangle(brush, rect);
                }
                using (var pen = new Pen(Color.Black))
                {
                    graphics.DrawRectangle(pen, rect.Left, rect.Top, rect.Width - 1, rect.Height - 1);
                }
            }
        }

        private static IEnumerable<Rectangle> GetHandleRects(Rectangle bounds)
        {
            var half = HandleSize / 2;
            var points = new[]
            {
                new Point(bounds.Left, bounds.Top),
                new Point(bounds.Left + bounds.Width / 2, bounds.Top),
                new Point(bounds.Right, bounds.Top),
                new Point(bounds.Left, bounds.Top + bounds.Height / 2),
                new Point(bounds.Right, bounds.Top + bounds.Height / 2),
                new Point(bounds.Left, bounds.Bottom),
                new Point(bounds.Left + bounds.Width / 2, bounds.Bottom),
                new Point(bounds.Right, bounds.Bottom)
            };
            foreach (var point in points)
            {
                yield return new Rectangle(point.X - half, point.Y - half, HandleSize, HandleSize);
            }
        }

        public void Select(Control control)
        {
            var previous = _selected;
            _selected = control;
            if (previous != null)
            {
                previous.Invalidate();
            }
            if (control != null)
            {
                control.Invalidate();
            }
            SelectionChanged?.Invoke(control);
        }

        public void UpdateModelProperty(Control control, string property, object value)
        {
            if (_model == null || control == null)
            {
                return;
            }
            var name = (string)control.Tag ?? "this";
            ControlInfo info;
            if (!_model.Controls.TryGetValue(name, out info))
            {
                return;
            }
            var propertyInfo = control.GetType().GetProperty(property);
            var type = propertyInfo != null ? propertyInfo.PropertyType : null;
            var expression = DesignerExpressionEvaluator.ToSourceExpression(value, type);
            if (expression == null)
            {
                return;
            }
            info.Properties[property] = expression;
            if (ReferenceEquals(control, _rootControl) && (property == "Size" || property == "ClientSize"))
            {
                SyncSurfaceToRoot();
            }
            else
            {
                EnsureSurfaceFits();
            }
            ModelChanged?.Invoke();
        }

        public void UpdateModelFromControl(Control control)
        {
            if (_model == null || control == null)
            {
                return;
            }
            var name = (string)control.Tag ?? "this";
            ControlInfo info;
            if (!_model.Controls.TryGetValue(name, out info))
            {
                return;
            }
            if (info.IsRoot)
            {
                info.Properties["ClientSize"] = DesignerExpressionEvaluator.ToSourceExpression(control.ClientSize, typeof(Size));
                info.Properties.Remove("Size");
                info.Properties.Remove("Location");
            }
            else
            {
                info.Properties["Location"] = DesignerExpressionEvaluator.ToSourceExpression(control.Location, typeof(Point));
                info.Properties["Size"] = DesignerExpressionEvaluator.ToSourceExpression(control.Size, typeof(Size));
            }
            ModelChanged?.Invoke();
        }
    }
}
