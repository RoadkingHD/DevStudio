using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DevStudio.Services;
using DevStudio.Theming;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;
using SW = System.Windows;
using SWC = System.Windows.Controls;

namespace DevStudio.Editor
{
    /// <summary>The code editor control: AvalonEdit hosted in WinForms, with completion,
    /// quick info tooltips, minimap, breakpoint margin and an editor context menu.</summary>
    public sealed class CodeEditorHost : System.Windows.Forms.UserControl
    {
        private readonly System.Windows.Forms.Integration.ElementHost _elementHost;
        private readonly SWC.Grid _grid;
        private readonly SWC.ColumnDefinition _minimapColumn;
        private readonly TextEditor _editor;
        private readonly MinimapControl _minimap;
        private readonly CompletionAdapter _completion = new CompletionAdapter();
        private readonly QuickInfoAdapter _quickInfo = new QuickInfoAdapter();
        private CompletionWindow _completionWindow;
        private System.Windows.Threading.DispatcherTimer _workspacePushTimer;
        private EditorDocument _document;
        private RoslynColorizer _roslynColorizer;
        private CppColorizer _cppColorizer;
        private DebugLineBackgroundRenderer _debugRenderer;
        private BreakpointMargin _breakpointMargin;
        private readonly SearchPanel _searchPanel;
        private System.Windows.Threading.DispatcherTimer _hoverTimer;
        private Point _lastMousePosition;

        public new event EventHandler TextChanged;
        public event EventHandler CaretMoved;
        public event EventHandler<int> BreakpointToggled;
        public event EventHandler GoToDefinitionRequested;
        public event EventHandler FindReferencesRequested;
        public event EventHandler FindRequested;

        public TextEditor InnerEditor
        {
            get { return _editor; }
        }

        public EditorDocument Document
        {
            get { return _document; }
        }

        public CodeEditorHost()
        {
            _editor = new TextEditor
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12.5,
                ShowLineNumbers = true,
                WordWrap = false,
                Options = { ConvertTabsToSpaces = true, IndentationSize = 4, EnableHyperlinks = false, EnableEmailHyperlinks = false }
            };

            _minimap = new MinimapControl { Editor = _editor };
            _minimapColumn = new SWC.ColumnDefinition { Width = new GridLength(110) };

            _grid = new SWC.Grid();
            _grid.Children.Add(_editor);
            _grid.Children.Add(_minimap);
            _grid.ColumnDefinitions.Add(new SWC.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _grid.ColumnDefinitions.Add(_minimapColumn);
            SWC.Grid.SetColumn(_editor, 0);
            SWC.Grid.SetColumn(_minimap, 1);

            _elementHost = new System.Windows.Forms.Integration.ElementHost
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                Child = _grid
            };
            Controls.Add(_elementHost);

            _hoverTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _hoverTimer.Tick += (s, e) =>
            {
                _hoverTimer.Stop();
                var hoverTask = OnMouseHoverAsync(_lastMousePosition);
            };
            _editor.TextArea.MouseMove += (s, e) =>
            {
                _lastMousePosition = e.GetPosition(_editor.TextArea.TextView);
                _hoverTimer.Stop();
                _hoverTimer.Start();
            };
            _editor.TextArea.MouseLeave += (s, e) => _hoverTimer.Stop();
            _editor.TextArea.TextEntered += OnTextEntered;
            _editor.TextArea.PreviewKeyDown += OnPreviewKeyDown;
            _editor.TextArea.Caret.PositionChanged += (s, e) => CaretMoved?.Invoke(this, EventArgs.Empty);
            _editor.TextChanged += (s, e) =>
            {
                _workspacePushTimer.Stop();
                _workspacePushTimer.Start();
                TextChanged?.Invoke(this, EventArgs.Empty);
            };
            _editor.TextArea.TextView.MouseRightButtonUp += OnRightClick;

            _workspacePushTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };
            _workspacePushTimer.Tick += (s, e) =>
            {
                _workspacePushTimer.Stop();
                PushToWorkspace();
            };

            _searchPanel = SearchPanel.Install(_editor.TextArea);

            BuildContextMenu();
            ApplyTheme(UiTheme.Current);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _workspacePushTimer?.Stop();
            }
            base.Dispose(disposing);
        }

        public void LoadDocument(EditorDocument document)
        {
            var textView = _editor.TextArea.TextView;
            if (_roslynColorizer != null)
            {
                textView.LineTransformers.Remove(_roslynColorizer);
                _roslynColorizer.ClassificationChanged -= OnClassificationChanged;
            }
            if (_cppColorizer != null)
            {
                textView.LineTransformers.Remove(_cppColorizer);
                _cppColorizer.ClassificationChanged -= OnClassificationChanged;
            }
            if (_debugRenderer != null)
            {
                textView.BackgroundRenderers.Remove(_debugRenderer);
            }
            if (_breakpointMargin != null)
            {
                _editor.TextArea.LeftMargins.Remove(_breakpointMargin);
            }

            _document = document;
            _editor.Document = document.Text;
            _editor.SyntaxHighlighting = null;

            if (document.Language == Projects.DevLanguage.CSharp)
            {
                _roslynColorizer = document.RoslynColorizer;
                _roslynColorizer.ClassificationChanged += OnClassificationChanged;
                textView.LineTransformers.Add(_roslynColorizer);
            }
            else
            {
                _cppColorizer = document.CppColorizer;
                _cppColorizer.ClassificationChanged += OnClassificationChanged;
                textView.LineTransformers.Add(_cppColorizer);
            }

            _debugRenderer = new DebugLineBackgroundRenderer(document);
            textView.BackgroundRenderers.Add(_debugRenderer);

            _breakpointMargin = new BreakpointMargin(document, line => BreakpointToggled?.Invoke(this, line));
            _breakpointMargin.SetTheme(UiTheme.Current);
            _editor.TextArea.LeftMargins.Add(_breakpointMargin);

            _editor.ScrollToHome();
            _editor.ScrollToLine(1);
            PushToWorkspace();
        }

        public void ApplyTheme(EditorTheme theme)
        {
            _editor.Background = new SolidColorBrush(theme.Background.ToMedia());
            _editor.Foreground = new SolidColorBrush(theme.Foreground.ToMedia());
            _editor.TextArea.SelectionBrush = new SolidColorBrush(theme.Selection.ToMedia());
            _editor.TextArea.SelectionBorder = null;
            _editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(theme.CurrentLine.ToMedia());
            _editor.TextArea.TextView.CurrentLineBorder = null;
            _minimap.InvalidateVisual();
            _breakpointMargin?.SetTheme(theme);
        }

        public void ShowMinimap(bool show)
        {
            _minimapColumn.Width = show ? new GridLength(110) : new GridLength(0);
        }

        public void SetDebugLine(int line)
        {
            if (_document != null)
            {
                _document.CurrentDebugLine = line;
                _editor.TextArea.TextView.Redraw();
                if (line > 0)
                {
                    _editor.ScrollToLine(line);
                }
            }
        }

        public void GotoLine(int line)
        {
            if (line < 1 || _document == null || line > _document.Text.LineCount)
            {
                return;
            }
            var lineObj = _document.Text.GetLineByNumber(line);
            _editor.CaretOffset = lineObj.Offset;
            _editor.ScrollToLine(line);
            _editor.Focus();
        }

        public void GotoOffset(int offset)
        {
            if (_document == null)
            {
                return;
            }
            _editor.CaretOffset = offset;
            var location = _document.Text.GetLocation(offset);
            _editor.ScrollToLine(location.Line);
            _editor.Focus();
        }

        public async void ShowCompletion(bool force)
        {
            if (_document == null || _document.Language != Projects.DevLanguage.CSharp || _completionWindow != null)
            {
                return;
            }

            try
            {
                var entries = await _completion.GetCompletionsAsync(_document, _editor.CaretOffset);
                if (entries.Count == 0 || _completionWindow != null)
                {
                    return;
                }

                _completionWindow = new CompletionWindow(_editor.TextArea)
                {
                    MinWidth = 220,
                    MaxHeight = 260
                };
                var window = _completionWindow;
                var data = entries.Select(e => (ICompletionData)new RoslynCompletionData(_editor, _document, e, _completion)).ToArray();
                var completionData = window.CompletionList.CompletionData;
                foreach (var item in data)
                {
                    completionData.Add(item);
                }
                window.Closed += (o, args) => _completionWindow = null;
                window.Show();
            }
            catch
            {
            }
        }

        private void OnTextEntered(object sender, TextCompositionEventArgs e)
        {
            if (_document == null)
            {
                return;
            }
            if (e.Text == "." && _document.Language == Projects.DevLanguage.CSharp)
            {
                ShowCompletion(false);
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && (Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                e.Handled = true;
                ShowCompletion(true);
            }
        }

        private async Task OnMouseHoverAsync(Point point)
        {
            if (_document == null)
            {
                return;
            }
            try
            {
                var documentPoint = point + _editor.TextArea.TextView.ScrollOffset;
                var position = _editor.TextArea.TextView.GetPositionFloor(documentPoint);
                if (!position.HasValue)
                {
                    return;
                }
                var line = _editor.Document.GetLineByNumber(position.Value.Line);
                var offset = _editor.Document.GetOffset(position.Value.Line, Math.Min(position.Value.Column, line.Length + 1));
                var info = await _quickInfo.GetQuickInfoAsync(_document, offset);
                if (!string.IsNullOrEmpty(info))
                {
                    var block = new SWC.TextBlock
                    {
                        Text = info,
                        MaxWidth = 480,
                        TextWrapping = TextWrapping.Wrap,
                        Background = new SolidColorBrush(UiTheme.Current.Panel.ToMedia()),
                        Foreground = new SolidColorBrush(UiTheme.Current.Text.ToMedia()),
                        FontFamily = new FontFamily("Consolas")
                    };
                    _editor.TextArea.ToolTip = block;
                }
                else
                {
                    _editor.TextArea.ToolTip = null;
                }
            }
            catch
            {
            }
        }

        private void OnRightClick(object sender, MouseButtonEventArgs e)
        {
            var menu = _editor.TextArea.ContextMenu;
            if (menu != null)
            {
                menu.PlacementTarget = _editor.TextArea;
                menu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void OnClassificationChanged(object sender, EventArgs e)
        {
            _editor.TextArea.TextView.Dispatcher.BeginInvoke(
                new Action(() => _editor.TextArea.TextView.Redraw()),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void PushToWorkspace()
        {
            if (_document == null || _document.Language != Projects.DevLanguage.CSharp)
            {
                return;
            }
            try
            {
                WorkspaceManager.Instance.AddOrUpdateDocument(_document.FilePath, _document.Text.Text);
            }
            catch
            {
            }
        }

        private void BuildContextMenu()
        {
            var menu = new SWC.ContextMenu();

            var cut = new SWC.MenuItem { Header = "Cut", InputGestureText = "Ctrl+X", Command = ApplicationCommands.Cut };
            var copy = new SWC.MenuItem { Header = "Copy", InputGestureText = "Ctrl+C", Command = ApplicationCommands.Copy };
            var paste = new SWC.MenuItem { Header = "Paste", InputGestureText = "Ctrl+V", Command = ApplicationCommands.Paste };
            var selectAll = new SWC.MenuItem { Header = "Select All", InputGestureText = "Ctrl+A", Command = ApplicationCommands.SelectAll };

            var sep1 = new SWC.Separator();
            var gotoDef = new SWC.MenuItem { Header = "Go To Definition", InputGestureText = "F12" };
            gotoDef.Click += (s, e) => GoToDefinitionRequested?.Invoke(this, EventArgs.Empty);
            var findRefs = new SWC.MenuItem { Header = "Find References", InputGestureText = "Shift+F12" };
            findRefs.Click += (s, e) => FindReferencesRequested?.Invoke(this, EventArgs.Empty);

            var sep2 = new SWC.Separator();
            var find = new SWC.MenuItem { Header = "Find...", InputGestureText = "Ctrl+F" };
            find.Click += (s, e) => FindRequested?.Invoke(this, EventArgs.Empty);

            menu.Items.Add(cut);
            menu.Items.Add(copy);
            menu.Items.Add(paste);
            menu.Items.Add(selectAll);
            menu.Items.Add(sep1);
            menu.Items.Add(gotoDef);
            menu.Items.Add(findRefs);
            menu.Items.Add(sep2);
            menu.Items.Add(find);

            _editor.TextArea.ContextMenu = menu;
        }

        private sealed class RoslynCompletionData : ICompletionData
        {
            private readonly TextEditor _editor;
            private readonly EditorDocument _doc;
            private readonly CompletionAdapter.CompletionEntry _entry;
            private readonly CompletionAdapter _adapter;

            public RoslynCompletionData(TextEditor editor, EditorDocument doc, CompletionAdapter.CompletionEntry entry, CompletionAdapter adapter)
            {
                _editor = editor;
                _doc = doc;
                _entry = entry;
                _adapter = adapter;
            }

            public ImageSource Image
            {
                get { return null; }
            }

            public string Text
            {
                get { return _entry.DisplayText; }
            }

            public object Content
            {
                get { return _entry.DisplayText; }
            }

            public object Description
            {
                get
                {
                    try
                    {
                        return Task.Run(() => _adapter.GetDescriptionAsync(_doc, _entry)).GetAwaiter().GetResult();
                    }
                    catch
                    {
                        return _entry.Kind ?? "";
                    }
                }
            }

            public double Priority
            {
                get { return 0; }
            }

            public void Complete(TextArea textArea, ISegment completionSegment, EventArgs invocationEventArgs)
            {
                string commit;
                try
                {
                    commit = Task.Run(() => _adapter.GetCommitTextAsync(_doc, _entry, textArea.Caret.Offset)).GetAwaiter().GetResult();
                }
                catch
                {
                    commit = _entry.DisplayText;
                }
                var typedLength = textArea.Caret.Offset - completionSegment.Offset;
                var replaceLength = Math.Max(0, typedLength);
                textArea.Document.Replace(completionSegment.Offset, replaceLength, commit);
            }
        }
    }

    /// <summary>Margin that shows breakpoint circles and toggles breakpoints on click.</summary>
    public sealed class BreakpointMargin : AbstractMargin
    {
        private readonly EditorDocument _doc;
        private readonly Action<int> _toggle;
        private EditorTheme _theme;
        private const double CircleWidth = 16;

        public BreakpointMargin(EditorDocument doc, Action<int> toggle)
        {
            _doc = doc;
            _toggle = toggle;
            _theme = UiTheme.Current;
        }

        public void SetTheme(EditorTheme theme)
        {
            _theme = theme;
            InvalidateVisual();
        }

        protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
        {
            if (oldTextView != null)
            {
                oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
            }
            if (newTextView != null)
            {
                newTextView.VisualLinesChanged += OnVisualLinesChanged;
            }
            base.OnTextViewChanged(oldTextView, newTextView);
            InvalidateVisual();
        }

        private void OnVisualLinesChanged(object sender, EventArgs e)
        {
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(Width, 0);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var textView = TextView;
            if (textView == null || _doc == null)
            {
                return;
            }
            var brush = new SolidColorBrush(_theme.BreakpointMark.ToMedia());
            foreach (var line in textView.VisualLines)
            {
                if (line.IsDisposed || line.TextLines.Count == 0)
                {
                    continue;
                }
                int number = line.FirstDocumentLine.LineNumber;
                if (_doc.BreakpointLines.Contains(number))
                {
                    double y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextMiddle);
                    dc.DrawEllipse(brush, null, new Point(CircleWidth / 2, y), 5.5, 5.5);
                }
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            var textView = TextView;
            if (textView == null || _doc == null)
            {
                return;
            }
            var pos = e.GetPosition(this);
            var line = textView.GetDocumentLineByVisualTop(pos.Y + textView.VerticalOffset);
            if (line != null)
            {
                _toggle?.Invoke(line.LineNumber);
                e.Handled = true;
            }
            base.OnMouseDown(e);
        }
    }

    /// <summary>Highlights the line where the debugger is currently stopped.</summary>
    public sealed class DebugLineBackgroundRenderer : IBackgroundRenderer
    {
        private readonly EditorDocument _doc;

        public DebugLineBackgroundRenderer(EditorDocument doc)
        {
            _doc = doc;
        }

        public KnownLayer Layer
        {
            get { return KnownLayer.Background; }
        }

        public void Draw(TextView textView, DrawingContext dc)
        {
            if (_doc == null || _doc.CurrentDebugLine <= 0)
            {
                return;
            }
            var line = textView.Document.GetLineByNumber(_doc.CurrentDebugLine);
            var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true };
            builder.AddSegment(textView, line);
            var geometry = builder.CreateGeometry();
            if (geometry != null)
            {
                var brush = new SolidColorBrush(Color.FromArgb(90, 255, 214, 0));
                dc.DrawGeometry(brush, null, geometry);
            }
        }
    }
}
