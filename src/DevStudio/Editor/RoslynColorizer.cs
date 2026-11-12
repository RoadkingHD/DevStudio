using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using DevStudio.Services;
using DevStudio.Theming;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

namespace DevStudio.Editor
{
    /// <summary>Syntax colorizer driven by Roslyn classified spans.</summary>
    public sealed class RoslynColorizer : IVisualLineTransformer
    {
        private readonly EditorDocument _doc;
        private EditorTheme _theme;
        private List<ClassifiedSpan> _cache;
        private ITextSourceVersion _cachedVersion;
        private bool _computing;
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
                _cache = null;
                _cachedVersion = null;
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

            if (!EnsureCache(version))
            {
                return;
            }

            var spans = _cache;
            if (spans == null)
            {
                return;
            }

            var lineStart = context.VisualLine.FirstDocumentLine.Offset;
            var lineEnd = context.VisualLine.LastDocumentLine.EndOffset;

            foreach (var element in elements)
            {
                var offset = lineStart + element.RelativeTextOffset;
                if (offset < lineEnd)
                {
                    var color = GetColorFor(spans, offset);
                    if (color != null)
                    {
                        var brush = new SolidColorBrush(color.Value.ToMedia());
                        brush.Freeze();
                        element.TextRunProperties.SetForegroundBrush(brush);
                    }
                }
            }
        }

        private bool EnsureCache(ITextSourceVersion version)
        {
            lock (_gate)
            {
                if (_cache != null && _cachedVersion == version)
                {
                    return true;
                }
            }

            if (_computing)
            {
                return _cache != null && _cachedVersion == version;
            }

            _computing = true;
            var requestedVersion = version;
            var document = WorkspaceManager.Instance.GetDocument(_doc.FilePath);
            if (document == null)
            {
                _computing = false;
                return false;
            }

            Task.Run(async () =>
            {
                try
                {
                    var model = await document.GetSemanticModelAsync();
                    if (model != null)
                    {
                        var spans = Classifier.GetClassifiedSpans(
                            model,
                            new TextSpan(0, model.SyntaxTree.Length),
                            document.Project.Solution.Workspace).ToList();
                        lock (_gate)
                        {
                            _cache = spans;
                            _cachedVersion = requestedVersion;
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    _computing = false;
                }

                ClassificationChanged?.Invoke(null, EventArgs.Empty);
            });

            return false;
        }

        private System.Drawing.Color? GetColorFor(List<ClassifiedSpan> spans, int offset)
        {
            int lo = 0, hi = spans.Count - 1;
            int found = -1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                var span = spans[mid].TextSpan;
                if (offset < span.Start)
                {
                    hi = mid - 1;
                }
                else if (offset >= span.End)
                {
                    lo = mid + 1;
                }
                else
                {
                    found = mid;
                    break;
                }
            }
            if (found < 0)
            {
                return null;
            }
            return MapColor(spans[found].ClassificationType);
        }

        private System.Drawing.Color? MapColor(string classificationType)
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
