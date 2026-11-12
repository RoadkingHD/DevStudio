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
        private DesignerModel _model;
        private Control _selected;
        private Control _rootControl;
        private bool _dragging;
        private Point _dragStart;
        private Rectangle _dragStartBounds;

        public event Action ModelChanged;
        public event Action<Control> SelectionChanged;

        public DesignSurface()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(150, 150, 155);
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
                _rootControl = new Panel { Size = new Size(width, height) };
            }
            _rootControl.Size = new Size(width, height);
            _rootControl.Location = new Point(0, 0);
            _rootControl.Enabled = false;
            _rootControl.Tag = "this";

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
                parent.Controls.Add(instance);
            }

            Controls.Add(_rootControl);
            _rootControl.BringToFront();

            ResumeLayout();
            Invalidate();
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
                    type = Type.GetType(info.TypeName);
                }
                if (type == null || !typeof(Control).IsAssignableFrom(type))
                {
                    return new Panel();
                }
                var instance = (Control)Activator.CreateInstance(type);
                instance.Name = info.IsRoot ? "" : info.Name;
                instance.Enabled = false;

                foreach (var property in info.Properties.Entries)
                {
                    ApplyProperty(instance, property.Key, property.Value);
                }
                return instance;
            }
            catch
            {
                return new Panel();
            }
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
            control.Paint += OnControlPaint;
        }

        private void OnControlMouseDown(object sender, MouseEventArgs e)
        {
            var control = (Control)sender;
            Select(control);
            _dragging = true;
            _dragStart = control.PointToScreen(e.Location);
            _dragStartBounds = control.Bounds;
            control.Capture = true;
        }

        private void OnControlMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || _selected == null)
            {
                return;
            }
            var control = (Control)sender;
            var screen = control.PointToScreen(e.Location);
            var delta = new Point(screen.X - _dragStart.X, screen.Y - _dragStart.Y);
            _selected.Bounds = new Rectangle(
                Math.Max(0, _dragStartBounds.X + delta.X),
                Math.Max(0, _dragStartBounds.Y + delta.Y),
                _dragStartBounds.Width,
                _dragStartBounds.Height);
        }

        private void OnControlMouseUp(object sender, MouseEventArgs e)
        {
            if (_dragging && _selected != null)
            {
                _dragging = false;
                ((Control)sender).Capture = false;
                UpdateModelFromControl(_selected);
            }
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
            info.Properties["Location"] = DesignerExpressionEvaluator.ToSourceExpression(control.Location, typeof(Point));
            info.Properties["Size"] = DesignerExpressionEvaluator.ToSourceExpression(control.Size, typeof(Size));
            ModelChanged?.Invoke();
        }
    }
}
