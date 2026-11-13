using System;
using System.Collections.Generic;
using System.Drawing;
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

            _surface.Dock = DockStyle.Fill;
            _surface.SelectionChanged += OnSelectionChanged;
            _surface.ModelChanged += OnModelChanged;

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
            rightSplit.Size = new Size(1150, 400);
            rightSplit.Orientation = Orientation.Vertical;
            rightSplit.FixedPanel = FixedPanel.Panel2;
            rightSplit.Panel1.Controls.Add(_split);
            rightSplit.Panel2.Controls.Add(_propertyGrid);
            rightSplit.SplitterDistance = 900;
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

        private sealed class ControlPropertyAdapter
        {
            private readonly DesignSurface _surface;
            private readonly Control _control;

            public ControlPropertyAdapter(DesignSurface surface, Control control)
            {
                _surface = surface;
                _control = control;
            }

            [System.ComponentModel.Browsable(true)]
            public string Name
            {
                get { return (string)_control.Tag ?? "(this)"; }
            }

            [System.ComponentModel.Browsable(true)]
            public string Text
            {
                get { return _control.Text; }
                set
                {
                    _control.Text = value;
                    _surface.UpdateModelProperty(_control, "Text", value);
                    _control.Invalidate();
                }
            }

            [System.ComponentModel.Browsable(true)]
            public Point Location
            {
                get { return _control.Location; }
                set
                {
                    _control.Location = value;
                    _surface.UpdateModelProperty(_control, "Location", value);
                }
            }

            [System.ComponentModel.Browsable(true)]
            public Size Size
            {
                get { return _control.Size; }
                set
                {
                    _control.Size = value;
                    _surface.UpdateModelProperty(_control, "Size", value);
                }
            }

            [System.ComponentModel.Browsable(true)]
            public bool Visible
            {
                get { return _control.Visible; }
                set
                {
                    _control.Visible = value;
                    _surface.UpdateModelProperty(_control, "Visible", value);
                }
            }

            [System.ComponentModel.Browsable(true)]
            public bool Enabled
            {
                get { return _control.Enabled; }
                set
                {
                    _control.Enabled = value;
                    _surface.UpdateModelProperty(_control, "Enabled", value);
                }
            }

            [System.ComponentModel.Browsable(true)]
            public Color BackColor
            {
                get { return _control.BackColor; }
                set
                {
                    _control.BackColor = value;
                    _surface.UpdateModelProperty(_control, "BackColor", value);
                }
            }

            [System.ComponentModel.Browsable(true)]
            public Color ForeColor
            {
                get { return _control.ForeColor; }
                set
                {
                    _control.ForeColor = value;
                    _surface.UpdateModelProperty(_control, "ForeColor", value);
                }
            }

            [System.ComponentModel.Browsable(true)]
            public Font Font
            {
                get { return _control.Font; }
                set
                {
                    _control.Font = value;
                    _surface.UpdateModelProperty(_control, "Font", value);
                }
            }
        }
    }
}
