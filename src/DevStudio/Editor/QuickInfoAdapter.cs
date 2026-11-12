using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using DevStudio.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DevStudio.Editor
{
    /// <summary>Builds syntax tooltips from Roslyn semantic information.</summary>
    public sealed class QuickInfoAdapter
    {
        public async Task<string> GetQuickInfoAsync(EditorDocument editorDoc, int offset)
        {
            var document = WorkspaceManager.Instance.GetDocument(editorDoc.FilePath);
            if (document == null)
            {
                return null;
            }

            var model = await document.GetSemanticModelAsync();
            var tree = await document.GetSyntaxTreeAsync();
            if (model == null || tree == null)
            {
                return null;
            }

            var root = await tree.GetRootAsync();
            var token = root.FindToken(Math.Max(0, Math.Min(offset, root.FullSpan.Length - 1)));
            var node = token.Parent;
            if (node == null)
            {
                return null;
            }

            SymbolInfo symbolInfo = model.GetSymbolInfo(node);
            var symbol = symbolInfo.Symbol;

            if (symbol == null)
            {
                var declared = model.GetDeclaredSymbol(node);
                symbol = declared;
            }

            if (symbol == null)
            {
                var typeInfo = model.GetTypeInfo(node);
                if (typeInfo.Type != null)
                {
                    return "Type: " + typeInfo.Type.ToDisplayString();
                }
                return null;
            }

            var sb = new StringBuilder();
            sb.Append(FormatKind(symbol.Kind.ToString()));
            sb.Append(" ");
            sb.AppendLine(symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

            var docComment = GetSummary(symbol);
            if (!string.IsNullOrEmpty(docComment))
            {
                sb.AppendLine();
                sb.Append(docComment);
            }

            var containing = symbol.ContainingType;
            if (containing != null && symbol.Kind != SymbolKind.NamedType)
            {
                sb.AppendLine();
                sb.Append("In: ").Append(containing.ToDisplayString());
            }
            return sb.ToString().TrimEnd();
        }

        private static string FormatKind(string kind)
        {
            switch (kind)
            {
                case "Local": return "local variable";
                case "Parameter": return "parameter";
                case "Field": return "field";
                case "Property": return "property";
                case "Event": return "event";
                case "Method": return "method";
                case "NamedType": return "type";
                case "Namespace": return "namespace";
                default: return kind.ToLowerInvariant();
            }
        }

        private static string GetSummary(ISymbol symbol)
        {
            try
            {
                var comment = symbol.GetDocumentationCommentXml();
                if (string.IsNullOrEmpty(comment))
                {
                    return null;
                }
                var doc = System.Xml.Linq.XDocument.Parse("<root>" + comment + "</root>");
                var summary = doc.Descendants("summary").FirstOrDefault();
                if (summary == null)
                {
                    return null;
                }
                return summary.Value.Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
