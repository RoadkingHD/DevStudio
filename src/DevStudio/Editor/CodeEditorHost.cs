using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevStudio.Projects;
using DevStudio.Services;
using DevStudio.Theming;
using DevStudio.UI;
using FastColoredTextBoxNS;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

namespace DevStudio.Editor
{
    /// <summary>The code editor control: FastColoredTextBox with themed syntax highlighting,
    /// Roslyn completion and quick info, minimap, breakpoints and an editor context menu.</summary>
    public sealed class CodeEditorHost : UserControl
    {
        private const int GutterWidth = 16;
        private readonly FastColoredTextBox _tb;
        private readonly DocumentMap _minimap;
        private readonly CompletionAdapter _completion = new CompletionAdapter();
        private readonly QuickInfoAdapter _quickInfo = new QuickInfoAdapter();
        private AutocompleteMenu _autocomplete;
        private EditorDocument _document;
        private System.Windows.Forms.Timer _workspacePushTimer;
        private TextStyle _defaultStyle;
        private TextStyle _keywordStyle;
        private TextStyle _controlKeywordStyle;
        private TextStyle _typeStyle;
        private TextStyle _stringStyle;
        private TextStyle _commentStyle;
        private TextStyle _numberStyle;
        private TextStyle _preprocessorStyle;
        private TextStyle _docCommentStyle;
        private TextStyle _semanticTypeStyle;
        private TextStyle _semanticInterfaceStyle;
        private TextStyle _semanticMethodStyle;
        private TextStyle _semanticFieldStyle;
        private TextStyle _semanticVariableStyle;
        private TextStyle _debugStyle;
        private int _debugLine = -1;
        private EditorTheme _theme;

        public new event EventHandler TextChanged;
        public event EventHandler CaretMoved;
        public event EventHandler<int> BreakpointToggled;
        public event EventHandler GoToDefinitionRequested;
        public event EventHandler FindReferencesRequested;
        public event EventHandler FindRequested;

        public Func<string, string> DebugValueResolver { get; set; }

        public EditorDocument Document
        {
            get { return _document; }
        }

        public int CaretOffset
        {
            get { return _tb != null ? _tb.SelectionStart : 0; }
            set
            {
                if (_tb == null)
                {
                    return;
                }
                _tb.SelectionStart = Math.Max(0, Math.Min(value, _tb.TextLength));
                _tb.DoSelectionVisible();
            }
        }

        public int CaretLine
        {
            get { return _tb != null ? _tb.Selection.Start.iLine + 1 : 1; }
        }

        public CodeEditorHost()
        {
            _tb = new FastColoredTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10f),
                ShowLineNumbers = true,
                WordWrap = false,
                TabLength = 4,
                AutoIndent = true,
                AutoIndentChars = false,
                HighlightingRangeType = HighlightingRangeType.VisibleRange,
                LeftPadding = 2
            };

            _minimap = new DocumentMap
            {
                Dock = DockStyle.Right,
                Width = 110,
                Target = _tb
            };

            Controls.Add(_tb);
            Controls.Add(_minimap);

            _workspacePushTimer = new System.Windows.Forms.Timer { Interval = 600 };
            _workspacePushTimer.Tick += (s, e) =>
            {
                _workspacePushTimer.Stop();
                PushToWorkspace();
            };

            _tb.TextChanged += OnTextBoxTextChanged;
            _tb.TextChangedDelayed += OnTextBoxTextChangedDelayed;
            _tb.SelectionChanged += (s, e) => CaretMoved?.Invoke(this, EventArgs.Empty);
            _tb.ToolTipNeeded += OnToolTipNeeded;
            _tb.MouseDown += OnEditorMouseDown;

            BuildContextMenu();
            ApplyTheme(UiTheme.Current);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _workspacePushTimer?.Stop();
                if (_autocomplete != null)
                {
                    _autocomplete.Close();
                    _autocomplete.Dispose();
                    _autocomplete = null;
                }
            }
            base.Dispose(disposing);
        }

        public void LoadDocument(EditorDocument document)
        {
            if (_autocomplete != null)
            {
                _autocomplete.Close();
            }
            _debugLine = -1;
            _document = document;

            _tb.Bookmarks.Clear();
            _tb.Language = document.Language == DevLanguage.Cpp ? Language.Custom : Language.CSharp;
            ApplyTheme(_theme ?? UiTheme.Current);

            _tb.Text = document.Text ?? "";

            foreach (var line in document.BreakpointLines)
            {
                if (line >= 1 && line <= _tb.LinesCount)
                {
                    _tb.Bookmarks.Add(line - 1);
                }
            }

            if (document.Language == DevLanguage.Cpp)
            {
                CppHighlight(_tb.Range);
            }
            else
            {
                ApplySemanticHighlightingAsync();
            }

            _tb.DoCaretVisible();
            PushToWorkspace();
        }

        public string GetText()
        {
            return _tb.Text;
        }

        public void SetText(string text)
        {
            _tb.Text = text ?? "";
        }

        public void GetCaretLocation(out int line, out int column)
        {
            var place = _tb.Selection.Start;
            line = place.iLine + 1;
            column = place.iChar + 1;
        }

        public void ShowMinimap(bool show)
        {
            _minimap.Visible = show;
        }

        public void ApplyTheme(EditorTheme theme)
        {
            _theme = theme;
            _tb.BackColor = theme.Background;
            _tb.ForeColor = theme.Foreground;
            _tb.IndentBackColor = theme.Background;
            _tb.PaddingBackColor = theme.Panel;
            _tb.LineNumberColor = theme.LineNumber;
            _tb.CaretColor = theme.Foreground;
            _tb.SelectionColor = theme.Selection;
            _tb.CurrentLineColor = theme.CurrentLine;
            _tb.ServiceLinesColor = UiTheme.PanelBorder;
            _tb.BookmarkColor = theme.BreakpointMark;
            _tb.DefaultStyle = _defaultStyle = new TextStyle(BrushOf(theme.Foreground), null, FontStyle.Regular);

            var highlighter = _tb.SyntaxHighlighter;
            _keywordStyle = new TextStyle(BrushOf(theme.Keyword), null, FontStyle.Regular);
            _controlKeywordStyle = new TextStyle(BrushOf(theme.ControlKeyword), null, FontStyle.Bold);
            _typeStyle = new TextStyle(BrushOf(theme.Type), null, FontStyle.Regular);
            _stringStyle = new TextStyle(BrushOf(theme.String), null, FontStyle.Regular);
            _commentStyle = new TextStyle(BrushOf(theme.Comment), null, FontStyle.Regular);
            _numberStyle = new TextStyle(BrushOf(theme.Number), null, FontStyle.Regular);
            _preprocessorStyle = new TextStyle(BrushOf(theme.Preprocessor), null, FontStyle.Regular);
            _docCommentStyle = new TextStyle(BrushOf(theme.XmlDoc), null, FontStyle.Regular);

            highlighter.KeywordStyle = _keywordStyle;
            highlighter.KeywordStyle2 = _controlKeywordStyle;
            highlighter.KeywordStyle3 = _controlKeywordStyle;
            highlighter.ClassNameStyle = _typeStyle;
            highlighter.TypesStyle = _typeStyle;
            highlighter.StringStyle = _stringStyle;
            highlighter.CommentStyle = _commentStyle;
            highlighter.NumberStyle = _numberStyle;
            highlighter.AttributeStyle = _preprocessorStyle;
            highlighter.CommentTagStyle = _docCommentStyle;
            highlighter.VariableStyle = _semanticVariableStyle;
            highlighter.FunctionsStyle = _semanticMethodStyle;

            _semanticTypeStyle = new TextStyle(BrushOf(theme.Type), null, FontStyle.Regular);
            _semanticInterfaceStyle = new TextStyle(BrushOf(theme.InterfaceName), null, FontStyle.Regular);
            _semanticMethodStyle = new TextStyle(BrushOf(theme.MethodName), null, FontStyle.Regular);
            _semanticFieldStyle = new TextStyle(BrushOf(theme.FieldName), null, FontStyle.Regular);
            _semanticVariableStyle = new TextStyle(BrushOf(theme.VariableName), null, FontStyle.Regular);

            _debugStyle = new TextStyle(null, new SolidBrush(DebugHighlightColor(theme)), FontStyle.Bold);

            _minimap.BackColor = theme.Background;
            _minimap.ForeColor = theme.Foreground;
            _minimap.NeedRepaint();

            _tb.Invalidate();
        }

        private static SolidBrush BrushOf(Color color)
        {
            return new SolidBrush(color);
        }

        private static Color DebugHighlightColor(EditorTheme theme)
        {
            double amount = theme.IsDark ? 0.6 : 0.45;
            return Color.FromArgb(
                (int)Math.Round(theme.CurrentStatement.R * amount + theme.Background.R * (1 - amount)),
                (int)Math.Round(theme.CurrentStatement.G * amount + theme.Background.G * (1 - amount)),
                (int)Math.Round(theme.CurrentStatement.B * amount + theme.Background.B * (1 - amount)));
        }

        public void SetDebugLine(int line)
        {
            if (_document == null)
            {
                return;
            }
            _document.CurrentDebugLine = line;
            if (_debugLine >= 0 && _debugLine < _tb.LinesCount)
            {
                _tb.GetLine(_debugLine).ClearStyle(new Style[] { _debugStyle });
            }
            _debugLine = line - 1;
            if (_debugLine >= 0 && _debugLine < _tb.LinesCount)
            {
                _tb.GetLine(_debugLine).SetStyle(_debugStyle);
                _tb.DoRangeVisible(_tb.GetLine(_debugLine), true);
            }
            _tb.Invalidate();
        }

        public void RefreshBreakpoints()
        {
            if (_document == null)
            {
                return;
            }
            _tb.Bookmarks.Clear();
            foreach (var line in _document.BreakpointLines)
            {
                if (line >= 1 && line <= _tb.LinesCount)
                {
                    _tb.Bookmarks.Add(line - 1);
                }
            }
            _tb.Invalidate();
        }

        public void GotoLine(int line)
        {
            if (_tb == null || line < 1 || line > _tb.LinesCount)
            {
                return;
            }
            _tb.Selection.Start = new Place(0, line - 1);
            _tb.DoSelectionVisible();
            _tb.Focus();
        }

        public void GotoOffset(int offset)
        {
            if (_tb == null)
            {
                return;
            }
            _tb.SelectionStart = Math.Max(0, Math.Min(offset, _tb.TextLength));
            _tb.DoSelectionVisible();
            _tb.Focus();
        }

        private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_document != null)
            {
                _document.Text = _tb.Text;
                _document.Dirty = true;
            }
            _workspacePushTimer.Stop();
            _workspacePushTimer.Start();

            if (_document != null && _document.Language == DevLanguage.Cpp)
            {
                CppHighlight(_tb.Range);
            }

            TextChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnTextBoxTextChangedDelayed(object sender, TextChangedEventArgs e)
        {
            if (_document == null)
            {
                return;
            }
            if (_document.Language == DevLanguage.CSharp)
            {
                ApplySemanticHighlightingAsync();
                ReapplyDebugLine();
                var place = _tb.Selection.Start;
                if (IsValidCompletionContext(place))
                {
                    ShowCompletion(false);
                }
            }
        }

        private void ReapplyDebugLine()
        {
            if (_debugLine >= 0 && _debugLine < _tb.LinesCount)
            {
                _tb.GetLine(_debugLine).SetStyle(_debugStyle);
            }
        }

        private void OnEditorMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.X <= GutterWidth)
            {
                var place = _tb.PointToPlace(new Point(_tb.LeftIndent, e.Y));
                BreakpointToggled?.Invoke(this, place.iLine + 1);
            }
            else if (e.Button == MouseButtons.Right)
            {
                var place = _tb.PointToPlace(e.Location);
                var start = _tb.Selection.Start;
                var end = _tb.Selection.End;
                bool insideSelection = !_tb.Selection.IsEmpty &&
                    place.iLine >= Math.Min(start.iLine, end.iLine) &&
                    place.iLine <= Math.Max(start.iLine, end.iLine);
                if (!insideSelection)
                {
                    _tb.Selection.BeginUpdate();
                    _tb.Selection.Start = place;
                    _tb.Selection.End = place;
                    _tb.Selection.EndUpdate();
                    CaretMoved?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void OnToolTipNeeded(object sender, ToolTipNeededEventArgs e)
        {
            if (_document == null || _document.Language != DevLanguage.CSharp || string.IsNullOrEmpty(e.HoveredWord))
            {
                return;
            }
            try
            {
                int offset = _tb.PlaceToPosition(e.Place);
                var info = Task.Run(() => _quickInfo.GetQuickInfoAsync(_document, offset)).GetAwaiter().GetResult();
                string debugValue = null;
                var resolver = DebugValueResolver;
                if (resolver != null)
                {
                    try { debugValue = resolver(e.HoveredWord); } catch { }
                }
                if (debugValue != null)
                {
                    e.ToolTipTitle = e.HoveredWord + " = " + debugValue;
                    e.ToolTipText = info;
                    e.ToolTipIcon = ToolTipIcon.None;
                    return;
                }
                if (!string.IsNullOrEmpty(info))
                {
                    int newline = info.IndexOf('\n');
                    e.ToolTipTitle = newline > 0 ? info.Substring(0, newline).Trim() : info.Trim();
                    e.ToolTipText = newline > 0 ? info.Substring(newline + 1).Trim() : null;
                    e.ToolTipIcon = ToolTipIcon.None;
                }
            }
            catch
            {
            }
        }

        private bool IsValidCompletionContext(Place place)
        {
            if (place.iChar == 0)
            {
                return false;
            }
            var lineText = _tb.GetLineText(place.iLine);
            if (place.iChar > lineText.Length)
            {
                return false;
            }
            char prev = lineText[place.iChar - 1];
            return char.IsLetterOrDigit(prev) || prev == '_' || prev == '.';
        }

        public async void ShowCompletion(bool forced)
        {
            if (_document == null || _document.Language != DevLanguage.CSharp || _tb.ReadOnly)
            {
                return;
            }
            var place = _tb.Selection.Start;
            if (!forced && !IsValidCompletionContext(place))
            {
                return;
            }
            try
            {
                EnsureAutocomplete();
                int caret = _tb.SelectionStart;
                var entries = await _completion.GetCompletionsAsync(_document, caret);
                if (_tb.IsDisposed || _document == null || entries.Count == 0 || _tb.SelectionStart != caret)
                {
                    return;
                }
                var items = new List<AutocompleteItem>();
                foreach (var entry in entries)
                {
                    items.Add(new RoslynAutocompleteItem(entry.DisplayText, entry.Kind));
                }
                _autocomplete.Items.SetAutocompleteItems(items);
                _autocomplete.Show(true);
            }
            catch
            {
            }
        }

        private void EnsureAutocomplete()
        {
            if (_autocomplete != null)
            {
                return;
            }
            _autocomplete = new AutocompleteMenu(_tb)
            {
                MinFragmentLength = 1,
                AppearInterval = 300,
                AllowTabKey = true,
                SearchPattern = @"[\w\.]",
                Font = new Font("Segoe UI", 9f)
            };
            _autocomplete.ImageList = StudioIcons.CreateCompletionImageList();
            _autocomplete.SelectedColor = UiTheme.Accent;
            _autocomplete.HoveredColor = UiTheme.PanelBorder;
            _autocomplete.Opening += (s, e) =>
            {
                if (!IsValidCompletionContext(_tb.Selection.Start))
                {
                    e.Cancel = true;
                }
            };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Space))
            {
                ShowCompletion(true);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void PushToWorkspace()
        {
            if (_document == null || _document.Language != DevLanguage.CSharp)
            {
                return;
            }
            try
            {
                WorkspaceManager.Instance.AddOrUpdateDocument(_document.FilePath, _tb.Text);
            }
            catch
            {
            }
        }

        private async void ApplySemanticHighlightingAsync()
        {
            if (_document == null || _document.Language != DevLanguage.CSharp)
            {
                return;
            }
            try
            {
                var document = WorkspaceManager.Instance.GetDocument(_document.FilePath);
                if (document == null)
                {
                    return;
                }
                int textLength = _tb.TextLength;
                var spans = await Classifier.GetClassifiedSpansAsync(document, new TextSpan(0, textLength), CancellationToken.None);
                if (_tb.IsDisposed || _document == null || _tb.TextLength != textLength)
                {
                    return;
                }
                _tb.Range.ClearStyle(new Style[] { _semanticTypeStyle, _semanticInterfaceStyle, _semanticMethodStyle, _semanticFieldStyle, _semanticVariableStyle, _controlKeywordStyle });
                foreach (var span in spans)
                {
                    var style = MapClassification(span);
                    if (style == null)
                    {
                        continue;
                    }
                    var start = _tb.PositionToPlace(span.TextSpan.Start);
                    var end = _tb.PositionToPlace(span.TextSpan.End);
                    _tb.GetRange(start, end).SetStyle(style);
                }
                ReapplyDebugLine();
                _tb.Invalidate();
            }
            catch
            {
            }
        }

        private Style MapClassification(ClassifiedSpan span)
        {
            switch (span.ClassificationType)
            {
                case "ControlKeyword": return _controlKeywordStyle;
                case "ClassName":
                case "RecordClassName":
                case "StructName":
                case "EnumName":
                case "DelegateName":
                case "TypeName": return _semanticTypeStyle;
                case "InterfaceName": return _semanticInterfaceStyle;
                case "MethodName":
                case "ExtensionMethodName": return _semanticMethodStyle;
                case "FieldName":
                case "PropertyName":
                case "EventName":
                case "ConstantName": return _semanticFieldStyle;
                case "LocalName":
                case "ParameterName":
                case "RangeVariableName": return _semanticVariableStyle;
                default: return null;
            }
        }

        private void CppHighlight(Range range)
        {
            try
            {
                range.ClearStyle(new Style[] { _commentStyle, _stringStyle, _numberStyle, _preprocessorStyle, _keywordStyle });
                range.SetStyle(_commentStyle, @"//.*$|/\*.*?\*/", RegexOptions.Multiline | RegexOptions.Singleline);
                range.SetStyle(_stringStyle, "@?\"(?:[^\"\\\\]|\\\\.)*\"|'(?:[^'\\\\]|\\\\.)*'", RegexOptions.Singleline);
                range.SetStyle(_preprocessorStyle, @"^\s*#\s*\w+.*$", RegexOptions.Multiline);
                range.SetStyle(_keywordStyle, CppKeywordsPattern, RegexOptions.IgnoreCase);
                range.SetStyle(_numberStyle, @"\b0[xX][0-9a-fA-F]+\b|\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?[fFuUlL]*\b");
                _tb.Invalidate();
            }
            catch
            {
            }
        }

        private static readonly string CppKeywordsPattern =
            @"\b(alignas|alignof|and|asm|auto|bool|break|case|catch|char|char8_t|char16_t|char32_t|class|compl|concept|const|consteval|constexpr|constinit|const_cast|continue|co_await|co_return|co_yield|decltype|default|delete|do|double|dynamic_cast|else|enum|explicit|export|extern|false|float|for|friend|goto|if|inline|int|long|mutable|namespace|new|noexcept|not|nullptr|operator|or|private|protected|public|register|reinterpret_cast|requires|return|short|signed|sizeof|static|static_assert|static_cast|struct|switch|template|this|thread_local|throw|true|try|typedef|typeid|typename|union|unsigned|using|virtual|void|volatile|wchar_t|while|xor|NULL)\b";

        private void BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            var cut = new ToolStripMenuItem("Cut", null, (s, e) => _tb.Cut()) { ShortcutKeyDisplayString = "Ctrl+X" };
            var copy = new ToolStripMenuItem("Copy", null, (s, e) => _tb.Copy()) { ShortcutKeyDisplayString = "Ctrl+C" };
            var paste = new ToolStripMenuItem("Paste", null, (s, e) => _tb.Paste()) { ShortcutKeyDisplayString = "Ctrl+V" };
            var selectAll = new ToolStripMenuItem("Select All", null, (s, e) => _tb.SelectAll()) { ShortcutKeyDisplayString = "Ctrl+A" };

            var gotoDef = new ToolStripMenuItem("Go To Definition", null, (s, e) => GoToDefinitionRequested?.Invoke(this, EventArgs.Empty)) { ShortcutKeyDisplayString = "F12" };
            var findRefs = new ToolStripMenuItem("Find References", null, (s, e) => FindReferencesRequested?.Invoke(this, EventArgs.Empty)) { ShortcutKeyDisplayString = "Shift+F12" };

            var find = new ToolStripMenuItem("Find...", null, (s, e) => FindRequested?.Invoke(this, EventArgs.Empty)) { ShortcutKeyDisplayString = "Ctrl+F" };

            menu.Opening += (s, e) =>
            {
                bool hasSelection = !_tb.Selection.IsEmpty;
                cut.Enabled = hasSelection;
                copy.Enabled = hasSelection;
                paste.Enabled = Clipboard.ContainsText();
            };

            menu.Items.Add(cut);
            menu.Items.Add(copy);
            menu.Items.Add(paste);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(selectAll);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(gotoDef);
            menu.Items.Add(findRefs);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(find);

            _tb.ContextMenuStrip = menu;
        }

        private sealed class RoslynAutocompleteItem : AutocompleteItem
        {
            private readonly string _kind;

            public RoslynAutocompleteItem(string text, string kind) : base(text)
            {
                _kind = kind;
                ToolTipTitle = kind ?? "";
                ImageIndex = StudioIcons.CompletionIndex(kind);
            }

            public override CompareResult Compare(string fragmentText)
            {
                if (string.IsNullOrEmpty(fragmentText))
                {
                    return CompareResult.Visible;
                }
                int dot = fragmentText.LastIndexOf('.');
                string member = dot >= 0 ? fragmentText.Substring(dot + 1) : fragmentText;
                if (member.Length == 0)
                {
                    return CompareResult.Visible;
                }
                if (Text.StartsWith(member, StringComparison.InvariantCultureIgnoreCase) && Text != member)
                {
                    return CompareResult.VisibleAndSelected;
                }
                if (Text.ToLowerInvariant().Contains(member.ToLowerInvariant()))
                {
                    return CompareResult.Visible;
                }
                return CompareResult.Hidden;
            }
        }
    }
}
