using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DevStudio.Theming;
using ICSharpCode.AvalonEdit;

namespace DevStudio.Editor
{
    /// <summary>Colored syntax minimap: a scaled down overview of the document with a viewport marker.</summary>
    public sealed class MinimapControl : FrameworkElement
    {
        public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
            "Editor", typeof(TextEditor), typeof(MinimapControl),
            new FrameworkPropertyMetadata(null, OnEditorChanged));

        public TextEditor Editor
        {
            get { return (TextEditor)GetValue(EditorProperty); }
            set { SetValue(EditorProperty, value); }
        }

        private const double LineHeight = 3.0;
        private double _scale = 1.0;
        private bool _dragging;
        private Typeface _typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        public MinimapControl()
        {
            SnapsToDevicePixels = true;
            ClipToBounds = true;
            MinWidth = 90;
            MaxWidth = 140;
            IsHitTestVisible = true;
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            Loaded += (s, e) => Attach();
        }

        private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var minimap = (MinimapControl)d;
            minimap.Attach();
        }

        private void Attach()
        {
            var editor = Editor;
            if (editor == null || editor.Document == null)
            {
                return;
            }
            editor.Document.TextChanged -= OnTextChanged;
            editor.Document.TextChanged += OnTextChanged;
            var textView = editor.TextArea.TextView;
            textView.ScrollOffsetChanged -= OnScrollChanged;
            textView.ScrollOffsetChanged += OnScrollChanged;
            InvalidateVisual();
        }

        private void OnTextChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(InvalidateVisual), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnScrollChanged(object sender, EventArgs e)
        {
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var editor = Editor;
            if (editor == null || editor.Document == null)
            {
                return;
            }

            var theme = UiTheme.Current;
            dc.DrawRectangle(new SolidColorBrush(theme.Background.ToMedia()), null, new Rect(0, 0, ActualWidth, ActualHeight));

            var doc = editor.Document;
            int lineCount = Math.Max(1, doc.LineCount);
            double availableHeight = Math.Max(10, ActualHeight);
            _scale = Math.Min(1.0, availableHeight / (lineCount * LineHeight));

            var backgroundBrush = new SolidColorBrush(theme.Background.ToMedia());
            var viewportColor = theme.MinimapViewport.ToMedia();
            var accentColor = theme.Accent.ToMedia();
            var viewportBrush = new SolidColorBrush(Color.FromArgb(60, viewportColor.R, viewportColor.G, viewportColor.B));
            var viewportPen = new Pen(new SolidColorBrush(Color.FromArgb(120, accentColor.R, accentColor.G, accentColor.B)), 1);

            double y = 4;
            for (int line = 1; line <= lineCount; line++)
            {
                var docLine = doc.GetLineByNumber(line);
                var text = doc.GetText(docLine.Offset, docLine.Length);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var trimmed = text.TrimStart();
                    double indent = (text.Length - trimmed.Length) * (2.0 * _scale);
                    var ft = new FormattedText(
                        trimmed,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        _typeface,
                        2.2 * _scale + 0.4,
                        ForegroundForLine(theme, trimmed),
                        null,
                        1.25);
                    dc.DrawText(ft, new Point(2 + Math.Min(indent, 30), y));
                }
                y += LineHeight * _scale;
                if (y > ActualHeight)
                {
                    break;
                }
            }

            var textView = editor.TextArea.TextView;
            double scrollable = Math.Max(0, textView.DocumentHeight - editor.ViewportHeight);
            if (scrollable > 0)
            {
                double ratioTop = Math.Max(0, Math.Min(1, textView.VerticalOffset / scrollable));
                double ratioSize = Math.Max(0, Math.Min(1, editor.ViewportHeight / textView.DocumentHeight));
                double vpTop = ratioTop * ActualHeight;
                double vpHeight = Math.Max(12, ratioSize * ActualHeight);
                dc.DrawRectangle(viewportBrush, viewportPen, new Rect(0, vpTop, ActualWidth, vpHeight));
            }
        }

        private Brush ForegroundForLine(EditorTheme theme, string line)
        {
            var t = line.TrimStart();
            if (t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("/*", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal))
            {
                return new SolidColorBrush(theme.Comment.ToMedia());
            }
            if (t.StartsWith("#", StringComparison.Ordinal))
            {
                return new SolidColorBrush(theme.Preprocessor.ToMedia());
            }
            return new SolidColorBrush(theme.Keyword.ToMedia()) { Opacity = 0.85 };
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragging = true;
            CaptureMouse();
            ScrollTo(e.GetPosition(this).Y);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragging)
            {
                ScrollTo(e.GetPosition(this).Y);
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _dragging = false;
            ReleaseMouseCapture();
        }

        private void ScrollTo(double y)
        {
            var editor = Editor;
            if (editor == null || editor.Document == null)
            {
                return;
            }
            var textView = editor.TextArea.TextView;
            double scrollable = Math.Max(0, textView.DocumentHeight - editor.ViewportHeight);
            if (scrollable <= 0)
            {
                return;
            }
            double ratio = Math.Max(0, Math.Min(1, y / ActualHeight));
            ((System.Windows.Controls.Primitives.IScrollInfo)textView).SetVerticalOffset(ratio * scrollable);
        }
    }
}
