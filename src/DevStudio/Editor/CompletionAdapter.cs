using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevStudio.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;

namespace DevStudio.Editor
{
    /// <summary>Code completion backed by the Roslyn completion service.</summary>
    public sealed class CompletionAdapter
    {
        public sealed class CompletionEntry
        {
            public string DisplayText { get; internal set; }
            public string Description { get; internal set; }
            public string Kind { get; internal set; }
            internal CompletionItem Item { get; set; }
            internal Document Document { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        public async Task<IList<CompletionEntry>> GetCompletionsAsync(EditorDocument editorDoc, int caretOffset)
        {
            var document = WorkspaceManager.Instance.GetDocument(editorDoc.FilePath);
            if (document == null)
            {
                return new CompletionEntry[0];
            }

            var service = CompletionService.GetService(document);
            if (service == null)
            {
                return new CompletionEntry[0];
            }

            var list = await service.GetCompletionsAsync(document, caretOffset);
            if (list == null || list.ItemsList.Count == 0)
            {
                return new CompletionEntry[0];
            }

            var result = new List<CompletionEntry>();
            foreach (var item in list.ItemsList.Take(400))
            {
                result.Add(new CompletionEntry
                {
                    DisplayText = item.DisplayText,
                    Kind = GetKind(item),
                    Item = item,
                    Document = document
                });
            }
            return result;
        }

        public async Task<string> GetCommitTextAsync(EditorDocument editorDoc, CompletionEntry entry, int caretOffset)
        {
            var document = WorkspaceManager.Instance.GetDocument(editorDoc.FilePath);
            if (document == null || entry.Item == null)
            {
                return entry.DisplayText;
            }

            var service = CompletionService.GetService(document);
            if (service == null)
            {
                return entry.DisplayText;
            }

            try
            {
                var change = await service.GetChangeAsync(document, entry.Item);
                if (change != null && change.TextChange != null)
                {
                    return change.TextChange.NewText ?? entry.DisplayText;
                }
            }
            catch
            {
            }
            return entry.DisplayText;
        }

        public async Task<string> GetDescriptionAsync(EditorDocument editorDoc, CompletionEntry entry)
        {
            var document = WorkspaceManager.Instance.GetDocument(editorDoc.FilePath);
            if (document == null || entry.Item == null)
            {
                return entry.Kind ?? "";
            }

            var service = CompletionService.GetService(document);
            if (service == null)
            {
                return entry.Kind ?? "";
            }

            try
            {
                var description = await service.GetDescriptionAsync(document, entry.Item);
                return description == null ? entry.Kind ?? "" : description.Text;
            }
            catch
            {
                return entry.Kind ?? "";
            }
        }

        private static string GetKind(CompletionItem item)
        {
            var tags = item.Tags;
            if (tags.Contains("Method")) return "Method";
            if (tags.Contains("Field")) return "Field";
            if (tags.Contains("Property")) return "Property";
            if (tags.Contains("Event")) return "Event";
            if (tags.Contains("Class")) return "Class";
            if (tags.Contains("Interface")) return "Interface";
            if (tags.Contains("Struct")) return "Struct";
            if (tags.Contains("Enum")) return "Enum";
            if (tags.Contains("Keyword")) return "Keyword";
            if (tags.Contains("Namespace")) return "Namespace";
            return "Identifier";
        }
    }
}
