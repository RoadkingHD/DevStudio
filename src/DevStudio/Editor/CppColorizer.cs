using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;
using DevStudio.Theming;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace DevStudio.Editor
{
    /// <summary>Regex based syntax colorizer for C/C++ sources.</summary>
    public sealed class CppColorizer : IVisualLineTransformer
    {
        private static readonly string[] Keywords =
        {
            "alignas","alignof","and","asm","auto","break","case","catch","class","const","constexpr","const_cast",
            "continue","decltype","default","delete","do","dynamic_cast","else","enum","explicit","export","extern",
            "false","for","friend","goto","if","inline","mutable","namespace","new","noexcept","not","nullptr",
            "operator","or","override","private","protected","public","register","reinterpret_cast","return",
            "sizeof","static","static_assert","static_cast","struct","switch","template","this","throw","true","try",
            "typedef","typeid","typename","union","using","virtual","volatile","while","xor","final"
        };

        private static readonly string[] TypeWords =
        {
            "bool","char","char8_t","char16_t","char32_t","double","float","int","long","short","signed","unsigned",
            "void","wchar_t","size_t","ssize_t","int8_t","int16_t","int32_t","int64_t","uint8_t","uint16_t","uint32_t",
            "uint64_t","FILE","std","string","vector","map","pair","cout","cin","endl"
        };

        private static readonly Regex WordRegex = new Regex(@"\b[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new Regex(@"\b(0[xX][0-9a-fA-F']+|\d[\d'.]*([eE][+-]?\d+)?)[fFuUlL]*\b", RegexOptions.Compiled);

        private readonly EditorDocument _doc;
        private EditorTheme _theme;
        private List<ColoredSegment> _cache;
        private ITextSourceVersion _cachedVersion;
        private bool _computing;
        private readonly object _gate = new object();

        public event EventHandler ClassificationChanged;

        public CppColorizer(EditorDocument doc, EditorTheme theme)
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
            var text = _doc.Text.Text;
            Task.Run(() =>
            {
                try
                {
                    var spans = Scan(text);
                    lock (_gate)
                    {
                        _cache = spans;
                        _cachedVersion = requestedVersion;
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

        private static List<ColoredSegment> Scan(string text)
        {
            var spans = new List<ColoredSegment>();
            int i = 0, n = text.Length;
            while (i < n)
            {
                char c = text[i];
                if (c == '/' && i + 1 < n && text[i + 1] == '/')
                {
                    var end = text.IndexOf('\n', i);
                    if (end < 0) end = n;
                    spans.Add(new ColoredSegment(i, end - i, Category.Comment));
                    i = end;
                }
                else if (c == '/' && i + 1 < n && text[i + 1] == '*')
                {
                    var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    end = end < 0 ? n : end + 2;
                    spans.Add(new ColoredSegment(i, end - i, Category.Comment));
                    i = end;
                }
                else if (c == '"' || c == '\'')
                {
                    var quote = c;
                    int j = i + 1;
                    while (j < n)
                    {
                        if (text[j] == '\\') { j += 2; continue; }
                        if (text[j] == quote) { j++; break; }
                        if (text[j] == '\n') { break; }
                        j++;
                    }
                    spans.Add(new ColoredSegment(i, Math.Min(j, n) - i, Category.String));
                    i = Math.Min(j, n);
                }
                else if (c == '#')
                {
                    var lineEnd = text.IndexOf('\n', i);
                    if (lineEnd < 0) lineEnd = n;
                    spans.Add(new ColoredSegment(i, lineEnd - i, Category.Preprocessor));
                    i = lineEnd;
                }
                else
                {
                    i++;
                }
            }

            foreach (Match m in WordRegex.Matches(text))
            {
                var word = m.Value;
                Category cat;
                if (Array.IndexOf(Keywords, word) >= 0)
                {
                    cat = Category.Keyword;
                }
                else if (Array.IndexOf(TypeWords, word) >= 0)
                {
                    cat = Category.Type;
                }
                else if (word.Length > 0 && char.IsUpper(word[0]))
                {
                    cat = Category.Type;
                }
                else
                {
                    continue;
                }
                spans.Add(new ColoredSegment(m.Index, m.Length, cat));
            }

            foreach (Match m in NumberRegex.Matches(text))
            {
                spans.Add(new ColoredSegment(m.Index, m.Length, Category.Number));
            }

            spans.Sort((a, b) => a.Start.CompareTo(b.Start));
            return spans;
        }

        private System.Drawing.Color? GetColorFor(List<ColoredSegment> spans, int offset)
        {
            int lo = 0, hi = spans.Count - 1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                var s = spans[mid];
                if (offset < s.Start)
                {
                    hi = mid - 1;
                }
                else if (offset >= s.Start + s.Length)
                {
                    lo = mid + 1;
                }
                else
                {
                    return MapColor(s.Category);
                }
            }
            return null;
        }

        private System.Drawing.Color MapColor(Category category)
        {
            switch (category)
            {
                case Category.Keyword: return _theme.Keyword;
                case Category.Type: return _theme.Type;
                case Category.Comment: return _theme.Comment;
                case Category.String: return _theme.String;
                case Category.Number: return _theme.Number;
                case Category.Preprocessor: return _theme.Preprocessor;
                default: return _theme.Identifier;
            }
        }

        private enum Category
        {
            Keyword,
            Type,
            Comment,
            String,
            Number,
            Preprocessor
        }

        private struct ColoredSegment
        {
            public readonly int Start;
            public readonly int Length;
            public readonly Category Category;

            public ColoredSegment(int start, int length, Category category)
            {
                Start = start;
                Length = length;
                Category = category;
            }
        }
    }
}
