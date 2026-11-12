using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using DevStudio.Services;
using DevStudio.Theming;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace DevStudio.Editor
{
    /// <summary>Syntax colorizer driven by Roslyn classified spans, with an instant
    /// token-based syntactic layer so the whole file is always colored.</summary>
    public sealed class RoslynColorizer : IVisualLineTransformer
    {
        private readonly EditorDocument _doc;
        private EditorTheme _theme;
        private System.Drawing.Color?[] _syntacticColors;
        private ITextSourceVersion _syntacticVersion;
        private System.Drawing.Color?[] _semanticColors;
        private ITextSourceVersion _semanticVersion;
        private bool _computing;
        private int _retries;
        private readonly object _gate = new object();

        public event EventHandler ClassificationChanged;

        public RoslynColorizer(EditorDocument doc, EditorTheme theme)
        {
            _doc = doc;
            _theme = theme;
        }

        public void SetTheme(EditorTheme theme)
        {
            _theme = theme;
            Invalidate();
        }

        public void Invalidate()
        {
            lock (_gate)
            {
                _syntacticColors = null;
                _syntacticVersion = null;
                _semanticColors = null;
                _semanticVersion = null;
            }
        }

        public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements)
        {
            if (context == null || context.VisualLine == null || context.Document == null || elements == null || elements.Count == 0)
            {
                return;
            }

            var version = context.Document.Version;
            if (version == null)
            {
                return;
            }

            EnsureSyntactic(version, context.Document.Text);
            EnsureSemantic(version);

            System.Drawing.Color?[] semantic = null;
            System.Drawing.Color?[] syntactic;
            lock (_gate)
            {
                if (_semanticVersion == version)
                {
                    semantic = _semanticColors;
                }
                syntactic = _syntacticColors;
            }

            var lineStart = context.VisualLine.FirstDocumentLine.Offset;
            var lineEnd = context.VisualLine.LastDocumentLine.EndOffset;

            foreach (var element in elements)
            {
                var offset = lineStart + element.RelativeTextOffset;
                if (offset >= lineEnd)
                {
                    continue;
                }
                System.Drawing.Color? color = null;
                if (semantic != null && offset < semantic.Length)
                {
                    color = semantic[offset];
                }
                if (color == null && syntactic != null && offset < syntactic.Length)
                {
                    color = syntactic[offset];
                }
                if (color != null)
                {
                    var brush = new SolidColorBrush(color.Value.ToMedia());
                    brush.Freeze();
                    element.TextRunProperties.SetForegroundBrush(brush);
                }
            }
        }

        private void EnsureSyntactic(ITextSourceVersion version, string text)
        {
            lock (_gate)
            {
                if (_syntacticColors != null && _syntacticVersion == version)
                {
                    return;
                }
            }
            var colors = BuildSyntacticColors(text);
            lock (_gate)
            {
                _syntacticColors = colors;
                _syntacticVersion = version;
            }
        }

        private System.Drawing.Color?[] BuildSyntacticColors(string text)
        {
            var colors = new System.Drawing.Color?[text == null ? 0 : text.Length];
            if (string.IsNullOrEmpty(text))
            {
                return colors;
            }
            try
            {
                var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.CSharp7_3));
                foreach (var token in tree.GetCompilationUnitRoot().DescendantTokens())
                {
                    var color = MapToken(token.Kind());
                    if (color != null)
                    {
                        Fill(colors, token.Span, color.Value);
                    }
                    foreach (var trivia in token.LeadingTrivia)
                    {
                        FillTrivia(colors, trivia);
                    }
                    foreach (var trivia in token.TrailingTrivia)
                    {
                        FillTrivia(colors, trivia);
                    }
                }
            }
            catch
            {
            }
            return colors;
        }

        private void FillTrivia(System.Drawing.Color?[] colors, SyntaxTrivia trivia)
        {
            var kind = trivia.Kind();
            if (kind == SyntaxKind.SingleLineCommentTrivia ||
                kind == SyntaxKind.MultiLineCommentTrivia ||
                kind == SyntaxKind.SingleLineDocumentationCommentTrivia ||
                kind == SyntaxKind.MultiLineDocumentationCommentTrivia)
            {
                Fill(colors, trivia.Span, _theme.Comment);
            }
        }

        private void Fill(System.Drawing.Color?[] colors, TextSpan span, System.Drawing.Color color)
        {
            var start = Math.Max(0, span.Start);
            var end = Math.Min(colors.Length, span.End);
            for (var i = start; i < end; i++)
            {
                colors[i] = color;
            }
        }

        private System.Drawing.Color? MapToken(SyntaxKind kind)
        {
            if (kind == SyntaxKind.CharacterLiteralToken ||
                kind.ToString().IndexOf("StringLiteral", StringComparison.Ordinal) >= 0 ||
                kind.ToString().IndexOf("InterpolatedString", StringComparison.Ordinal) >= 0)
            {
                return _theme.String;
            }
            if (kind == SyntaxKind.NumericLiteralToken)
            {
                return _theme.Number;
            }
            if (SyntaxFacts.IsKeywordKind(kind))
            {
                return IsControlKeyword(kind) ? _theme.ControlKeyword : _theme.Keyword;
            }
            if (SyntaxFacts.IsPunctuation(kind))
            {
                return _theme.Punctuation;
            }
            if (IsOperatorToken(kind))
            {
                return _theme.Operator;
            }
            return null;
        }

        private static bool IsControlKeyword(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.IfKeyword:
                case SyntaxKind.ElseKeyword:
                case SyntaxKind.ForKeyword:
                case SyntaxKind.ForEachKeyword:
                case SyntaxKind.WhileKeyword:
                case SyntaxKind.DoKeyword:
                case SyntaxKind.SwitchKeyword:
                case SyntaxKind.ReturnKeyword:
                case SyntaxKind.BreakKeyword:
                case SyntaxKind.ContinueKeyword:
                case SyntaxKind.ThrowKeyword:
                case SyntaxKind.TryKeyword:
                case SyntaxKind.CatchKeyword:
                case SyntaxKind.FinallyKeyword:
                case SyntaxKind.GotoKeyword:
                case SyntaxKind.LockKeyword:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsOperatorToken(SyntaxKind kind)
        {
            var name = kind.ToString();
            if (!name.EndsWith("Token", StringComparison.Ordinal) || name.Contains("Literal"))
            {
                return false;
            }
            return !SyntaxFacts.IsKeywordKind(kind) &&
                   !SyntaxFacts.IsPunctuation(kind) &&
                   !name.StartsWith("Identifier", StringComparison.Ordinal) &&
                   !name.StartsWith("Contextual", StringComparison.Ordinal) &&
                   !name.StartsWith("Omitted", StringComparison.Ordinal) &&
                   !name.StartsWith("Eof", StringComparison.Ordinal) &&
                   !name.StartsWith("Xml", StringComparison.Ordinal) &&
                   !name.StartsWith("EndOfDirective", StringComparison.Ordinal) &&
                   !name.StartsWith("Hash", StringComparison.Ordinal) &&
                   !name.StartsWith("Interpolated", StringComparison.Ordinal) &&
                   !name.StartsWith("SingleLine", StringComparison.Ordinal) &&
                   !name.StartsWith("MultiLine", StringComparison.Ordinal) &&
                   !name.StartsWith("Documentation", StringComparison.Ordinal) &&
                   !name.StartsWith("Xml", StringComparison.Ordinal);
        }

        private void EnsureSemantic(ITextSourceVersion version)
        {
            lock (_gate)
            {
                if (_semanticColors != null && _semanticVersion == version)
                {
                    return;
                }
                if (_computing)
                {
                    return;
                }
                _computing = true;
            }

            var document = WorkspaceManager.Instance.GetDocument(_doc.FilePath);
            if (document == null)
            {
                lock (_gate)
                {
                    _computing = false;
                }
                ScheduleSemanticRetry();
                return;
            }

            var requestedVersion = version;
            Task.Run(async () =>
            {
                try
                {
                    var model = await document.GetSemanticModelAsync().ConfigureAwait(false);
                    if (model != null)
                    {
                        var spans = await Classifier.GetClassifiedSpansAsync(
                            document,
                            new TextSpan(0, model.SyntaxTree.Length),
                            CancellationToken.None).ConfigureAwait(false);
                        var list = spans.ToList();
                        var colors = new System.Drawing.Color?[_doc.Text.TextLength];
                        foreach (var span in list)
                        {
                            if (span.ClassificationType == "identifier")
                            {
                                Fill(colors, span.TextSpan, MapColor(span.ClassificationType));
                            }
                        }
                        foreach (var span in list)
                        {
                            if (span.ClassificationType != "identifier")
                            {
                                Fill(colors, span.TextSpan, MapColor(span.ClassificationType));
                            }
                        }
                        lock (_gate)
                        {
                            _semanticColors = colors;
                            _semanticVersion = requestedVersion;
                            _retries = 0;
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    lock (_gate)
                    {
                        _computing = false;
                    }
                }
                ClassificationChanged?.Invoke(null, EventArgs.Empty);
            });
        }

        private void ScheduleSemanticRetry()
        {
            if (Interlocked.Increment(ref _retries) > 40)
            {
                return;
            }
            Task.Delay(250).ContinueWith(t =>
            {
                ClassificationChanged?.Invoke(null, EventArgs.Empty);
            });
        }

        private System.Drawing.Color MapColor(string classificationType)
        {
            var t = _theme;
            switch (classificationType)
            {
                case "keyword":
                    return t.Keyword;
                case "control keyword":
                    return t.ControlKeyword;
                case "number":
                    return t.Number;
                case "string":
                case "verbatim string":
                case "interpolated string":
                case "character literal":
                    return t.String;
                case "comment":
                    return t.Comment;
                case "xml doc comment - text":
                case "xml doc comment - delimiter":
                case "xml doc comment - name":
                case "xml doc comment - attribute":
                case "xml doc comment - punctuation":
                case "xml doc comment - comment":
                    return t.XmlDoc;
                case "class name":
                case "struct name":
                case "enum name":
                case "delegate name":
                case "record name":
                case "type parameter name":
                    return t.Type;
                case "interface name":
                    return t.InterfaceName;
                case "method name":
                case "extension method name":
                    return t.MethodName;
                case "field name":
                case "constant name":
                case "enum member name":
                    return t.FieldName;
                case "local name":
                case "parameter name":
                case "range variable name":
                    return t.VariableName;
                case "property name":
                case "event name":
                case "namespace name":
                    return t.Type;
                case "preprocessor keyword":
                    return t.Preprocessor;
                case "punctuation":
                    return t.Punctuation;
                case "operator":
                case "operator overload":
                    return t.Operator;
                default:
                    return t.Identifier;
            }
        }
    }
}
