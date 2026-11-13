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

        public DesignSurface()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(150, 150, 155);
            MouseDown += (s, e) =>
            {
                if (_rootControl != null)
                {
                    Select(_rootControl);
                }
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
            var clientSize = model.Root.Properties["ClientSize"];
            if (clientSize != null)
            {
                var size = (Size?)DesignerExpressionEvaluator.Evaluate(clientSize, typeof(Size));
                if (size.HasValue)
                {
                    width = size.Value.Width;
                    height = size.Value.Height;
                }
            }
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

            _rootControl = CreateControlFor(model.Root);
            if (_rootControl == null)
            {
                _rootControl = CreatePlaceholder(width, height);
            }
            _rootControl.Size = new Size(width, height);
            _rootControl.Location = new Point(0, 0);
            _rootControl.Tag = "this";
            WireEvents(_rootControl);
            Location = new Point(12, 12);
            Size = new Size(width + 24, height + 24);

            foreach (var control in model.Controls.Values.Where(c => !c.IsRoot))
            {
                var instance = CreateControlFor(control);
                if (instance == null)
                {
                    continue;
                }
                instance.Tag = control.Name;
                WireEvents(instance);
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
                if (form != null)
                {
                    form.TopLevel = false;
                    form.FormBorderStyle = FormBorderStyle.None;
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

        private void WireEvents(Control control)
        {
            control.MouseDown += OnControlMouseDown;
            control.MouseMove += OnControlMouseMove;
            control.MouseUp += OnControlMouseUp;
            control.MouseLeave += OnControlMouseLeave;
            control.Paint += OnControlPaint;
        }

        private void OnControlMouseDown(object sender, MouseEventArgs e)
        {
            var control = (Control)sender;
            Select(control);
            if (e.Clicks >= 2)
            {
                DefaultEventRequested?.Invoke(control);
                return;
            }
            var handle = GetHandleAt(control.ClientRectangle, e.Location);
            if (handle != ResizeHandle.None)
            {
                _resizing = handle;
                _dragging = false;
                _dragStart = control.PointToScreen(e.Location);
                _dragStartBounds = control.Bounds;
                control.Capture = true;
                return;
            }
            if (ReferenceEquals(control, _rootControl))
            {
                return;
            }
            _dragging = true;
            _dragStart = control.PointToScreen(e.Location);
            _dragStartBounds = control.Bounds;
            control.Capture = true;
        }

        private void OnControlMouseMove(object sender, MouseEventArgs e)
        {
            var control = (Control)sender;
            if (_resizing != ResizeHandle.None && _selected != null)
            {
                var screen = control.PointToScreen(e.Location);
                var delta = new Point(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
                ApplyResize(delta);
                return;
            }
            if (_dragging && _selected != null)
            {
                var screen = control.PointToScreen(e.Location);
                var delta = new Point(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
                _selected.Bounds = new Rectangle(
                    Math.Max(0, _dragStartBounds.X + delta.X),
                    Math.Max(0, _dragStartBounds.Y + delta.Y),
                    _dragStartBounds.Width,
                    _dragStartBounds.Height);
                return;
            }
            if (ReferenceEquals(control, _selected))
            {
                control.Cursor = GetCursor(GetHandleAt(control.ClientRectangle, e.Location));
            }
        }

        private void OnControlMouseLeave(object sender, EventArgs e)
        {
            if (_dragging || _resizing != ResizeHandle.None)
            {
                return;
            }
            ((Control)sender).Cursor = null;
        }

        private void OnControlMouseUp(object sender, MouseEventArgs e)
        {
            if (_resizing != ResizeHandle.None && _selected != null)
            {
                _resizing = ResizeHandle.None;
                var resized = (Control)sender;
                resized.Capture = false;
                resized.Cursor = null;
                UpdateModelFromControl(_selected);
                SelectionChanged?.Invoke(_selected);
                return;
            }
            if (_dragging && _selected != null)
            {
                _dragging = false;
                ((Control)sender).Capture = false;
                UpdateModelFromControl(_selected);
                SelectionChanged?.Invoke(_selected);
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

        private void OnControlPaint(object sender, PaintEventArgs e)
        {
            var control = (Control)sender;
            if (ReferenceEquals(control, _selected))
            {
                using (var pen = new Pen(Color.FromArgb(0, 122, 204), 1.5f))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, control.Width - 1, control.Height - 1);
                }
                foreach (var rect in GetHandleRects(control.ClientRectangle))
                {
                    using (var brush = new SolidBrush(Color.White))
                    {
                        e.Graphics.FillRectangle(brush, rect);
                    }
                    using (var pen = new Pen(Color.Black))
                    {
                        e.Graphics.DrawRectangle(pen, rect.Left, rect.Top, rect.Width - 1, rect.Height - 1);
                    }
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
            info.Properties[property] = DesignerExpressionEvaluator.ToSourceExpression(value, type);
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
