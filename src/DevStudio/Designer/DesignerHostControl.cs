using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace DevStudio.Designer
{
    /// <summary>WinForms designer view: toolbox, design surface and property grid,
    /// synchronized with the InitializeComponent method of the edited source file.</summary>
    public sealed class DesignerHostControl : UserControl
    {
        private readonly DesignSurface _surface = new DesignSurface();
        private readonly ListBox _toolbox = new ListBox();
        private readonly PropertyGrid _propertyGrid = new PropertyGrid();
        private readonly ToolStrip _toolbar = new ToolStrip();
        private readonly SplitContainer _split = new SplitContainer();
        private string _sourceText;
        private bool _updating;

        public event Action<string> SourceChanged;
        public event Action ViewCodeRequested;
        public event Action<string> DiagnosticOutput;
        public event Action<string> EventHandlerNeeded;

        private static readonly string[] ToolboxControls =
        {
            "Button", "Label", "TextBox", "CheckBox", "RadioButton", "ListBox", "ComboBox",
            "PictureBox", "Panel", "GroupBox", "TreeView", "ListView", "ProgressBar",
            "DateTimePicker", "RichTextBox", "NumericUpDown", "LinkLabel", "TrackBar"
        };

        public DesignerHostControl()
        {
            _toolbar.GripStyle = ToolStripGripStyle.Hidden;
            var viewCode = new ToolStripButton("View Code");
            viewCode.Click += (s, e) => ViewCodeRequested?.Invoke();
            var apply = new ToolStripButton("Apply To Source");
            apply.Click += (s, e) => ApplyToSource();
            _toolbar.Items.Add(viewCode);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(apply);

            _toolbox.Dock = DockStyle.Fill;
            foreach (var name in ToolboxControls)
            {
                _toolbox.Items.Add(name);
            }
            _toolbox.SelectedIndexChanged += (s, e) => AddSelectedToolboxControl();

            var toolboxPanel = new Panel { Dock = DockStyle.Fill };
            toolboxPanel.Controls.Add(_toolbox);

            _surface.Location = new Point(12, 12);
            _surface.Size = new Size(664, 504);
            _surface.SelectionChanged += OnSelectionChanged;
            _surface.ModelChanged += OnModelChanged;
            _surface.Diagnostic += message => DiagnosticOutput?.Invoke(message);
            _surface.DefaultEventRequested += OnDefaultEventRequested;

            _propertyGrid.Dock = DockStyle.Fill;
            _propertyGrid.ToolbarVisible = false;

            var surfacePanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            surfacePanel.Controls.Add(_surface);

            _split.Size = new Size(900, 400);
            _split.Orientation = Orientation.Vertical;
            _split.FixedPanel = FixedPanel.Panel1;
            _split.Panel1.Controls.Add(toolboxPanel);
            _split.Panel2.Controls.Add(surfacePanel);
            _split.SplitterDistance = 160;
            _split.Dock = DockStyle.Fill;

            var rightSplit = new SplitContainer();
            rightSplit.Size = new Size(1200, 400);
            rightSplit.Orientation = Orientation.Vertical;
            rightSplit.FixedPanel = FixedPanel.Panel2;
            rightSplit.Panel1MinSize = 200;
            rightSplit.Panel2MinSize = 240;
            rightSplit.Panel1.Controls.Add(_split);
            rightSplit.Panel2.Controls.Add(_propertyGrid);
            rightSplit.SplitterDistance = 950;
            rightSplit.Dock = DockStyle.Fill;

            Controls.Add(_toolbar);
            Controls.Add(rightSplit);

            _toolbar.Dock = DockStyle.Top;
            rightSplit.BringToFront();
        }

        public void LoadSource(string sourceText)
        {
            _sourceText = sourceText;
            _updating = true;
            var model = InitializeComponentParser.Parse(sourceText);
            _surface.LoadModel(model.Valid ? model : null);
            _updating = false;
            _surface.Select(_surface.RootControl);
        }

        private void OnSelectionChanged(Control control)
        {
            if (control != null)
            {
                _propertyGrid.SelectedObject = new ControlPropertyAdapter(_surface, control);
            }
        }

        private void OnDefaultEventRequested(Control control)
        {
            if (control == null || _surface.Model == null || !_surface.Model.Valid)
            {
                return;
            }
            var name = (string)control.Tag ?? "this";
            ControlInfo info;
            if (!_surface.Model.Controls.TryGetValue(name, out info))
            {
                return;
            }
            var eventName = GetDefaultEventName(control);
            var baseName = info.IsRoot
                ? (string.IsNullOrEmpty(_surface.Model.ClassName) ? "Form1" : _surface.Model.ClassName)
                : name;
            var handlerName = baseName + "_" + eventName;
            if (!info.Events.Contains(eventName))
            {
                info.Events[eventName] = "new System.EventHandler(this." + handlerName + ")";
                ApplyToSource();
            }
            if (EventHandlerNeeded != null)
            {
                EventHandlerNeeded(handlerName);
            }
            ViewCodeRequested?.Invoke();
        }

        private static string GetDefaultEventName(Control control)
        {
            if (control is Form)
            {
                return "Load";
            }
            switch (control.GetType().Name)
            {
                case "TextBox":
                case "RichTextBox":
                    return "TextChanged";
                case "CheckBox":
                case "RadioButton":
                    return "CheckedChanged";
                case "ComboBox":
                case "ListBox":
                    return "SelectedIndexChanged";
                case "NumericUpDown":
                case "DateTimePicker":
                    return "ValueChanged";
                case "TrackBar":
                    return "Scroll";
                case "LinkLabel":
                    return "LinkClicked";
                default:
                    return "Click";
            }
        }

        private void OnModelChanged()
        {
            if (!_updating)
            {
                ApplyToSource();
            }
        }

        private void AddSelectedToolboxControl()
        {
            if (_toolbox.SelectedItem == null || _surface.Model == null || !_surface.Model.Valid)
            {
                return;
            }
            var typeName = "System.Windows.Forms." + _toolbox.SelectedItem;
            var baseName = _toolbox.SelectedItem.ToString();
            string name = baseName.ToLowerInvariant();
            int suffix = 1;
            while (_surface.Model.Controls.ContainsKey(name + suffix))
            {
                suffix++;
            }
            name = name + suffix;

            var info = new ControlInfo
            {
                Name = name,
                TypeName = typeName
            };
            info.Properties["Location"] = "new System.Drawing.Point(30, 30)";
            info.Properties["Size"] = baseName == "Button" || baseName == "TextBox"
                ? "new System.Drawing.Size(100, 23)"
                : "new System.Drawing.Size(120, 100)";
            info.Properties["Text"] = "\"" + baseName + "\"";
            _surface.Model.Controls[name] = info;
            _surface.Model.Root.Children.Add(name);

            ApplyToSource();
            ReloadSurface();
            _toolbox.SelectedIndex = -1;
        }

        private void ReloadSurface()
        {
            _updating = true;
            var model = InitializeComponentParser.Parse(_sourceText);
            _surface.LoadModel(model.Valid ? model : null);
            _updating = false;
        }

        public void ApplyToSource()
        {
            if (_surface.Model == null || !_surface.Model.Valid)
            {
                return;
            }
            var newText = InitializeComponentWriter.Apply(_sourceText, _surface.Model);
            if (newText != _sourceText)
            {
                _sourceText = newText;
                SourceChanged?.Invoke(_sourceText);
            }
        }

        private sealed class ControlPropertyAdapter : ICustomTypeDescriptor
        {
            private static readonly HashSet<string> HiddenProperties = new HashSet<string>(StringComparer.Ordinal)
            {
                "Tag", "DataBindings", "Controls", "Parent", "Site"
            };

            private readonly DesignSurface _surface;
            private readonly Control _control;

            public ControlPropertyAdapter(DesignSurface surface, Control control)
            {
                _surface = surface;
                _control = control;
            }

            AttributeCollection ICustomTypeDescriptor.GetAttributes()
            {
                return TypeDescriptor.GetAttributes(_control);
            }

            string ICustomTypeDescriptor.GetClassName()
            {
                return _control.GetType().Name;
            }

            string ICustomTypeDescriptor.GetComponentName()
            {
                return (string)_control.Tag ?? "(root)";
            }

            TypeConverter ICustomTypeDescriptor.GetConverter()
            {
                return TypeDescriptor.GetConverter(_control);
            }

            EventDescriptor ICustomTypeDescriptor.GetDefaultEvent()
            {
                return TypeDescriptor.GetDefaultEvent(_control);
            }

            PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty()
            {
                return TypeDescriptor.GetDefaultProperty(_control);
            }

            object ICustomTypeDescriptor.GetEditor(Type editorBaseType)
            {
                return TypeDescriptor.GetEditor(_control, editorBaseType);
            }

            EventDescriptorCollection ICustomTypeDescriptor.GetEvents()
            {
                return new EventDescriptorCollection(new EventDescriptor[0]);
            }

            EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes)
            {
                return new EventDescriptorCollection(new EventDescriptor[0]);
            }

            PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties()
            {
                return ((ICustomTypeDescriptor)this).GetProperties(null);
            }

            PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes)
            {
                var descriptors = new List<PropertyDescriptor>();
                foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(_control, attributes))
                {
                    if (!property.IsBrowsable || HiddenProperties.Contains(property.Name))
                    {
                        continue;
                    }
                    descriptors.Add(new ModelPropertyDescriptor(property, _surface, _control));
                }
                return new PropertyDescriptorCollection(descriptors.ToArray());
            }

            object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd)
            {
                return this;
            }
        }

        private sealed class ModelPropertyDescriptor : PropertyDescriptor
        {
            private readonly PropertyDescriptor _base;
            private readonly DesignSurface _surface;
            private readonly Control _control;

            public ModelPropertyDescriptor(PropertyDescriptor baseDescriptor, DesignSurface surface, Control control)
                : base(baseDescriptor, null)
            {
                _base = baseDescriptor;
                _surface = surface;
                _control = control;
            }

            public override Type ComponentType
            {
                get { return _base.ComponentType; }
            }

            public override Type PropertyType
            {
                get { return _base.PropertyType; }
            }

            public override bool IsReadOnly
            {
                get { return _base.IsReadOnly; }
            }

            public override bool CanResetValue(object component)
            {
                return _base.CanResetValue(_control);
            }

            public override object GetValue(object component)
            {
                return _base.GetValue(_control);
            }

            public override void ResetValue(object component)
            {
                _base.ResetValue(_control);
                PushValue();
                _control.Invalidate();
            }

            public override void SetValue(object component, object value)
            {
                if (!_surface.IsDesignBlocked(_control, Name))
                {
                    try
                    {
                        _base.SetValue(_control, value);
                    }
                    catch
                    {
                    }
                }
                PushValue();
                _control.Invalidate();
            }

            public override bool ShouldSerializeValue(object component)
            {
                return _base.ShouldSerializeValue(_control);
            }

            private void PushValue()
            {
                _surface.UpdateModelProperty(_control, Name, GetValue(null));
            }
        }
    }
}
