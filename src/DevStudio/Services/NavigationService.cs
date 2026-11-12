using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace DevStudio.Services
{
    public sealed class SymbolLocation
    {
        public string FilePath { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Description { get; set; }
    }

    /// <summary>Go To Definition / Find References implemented on top of Roslyn.</summary>
    public sealed class NavigationService
    {
        public async Task<SymbolLocation> GoToDefinitionAsync(string filePath, int offset)
        {
            var document = WorkspaceManager.Instance.GetDocument(filePath);
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
            var token = root.FindToken(offset);
            var node = token.Parent;
            if (node == null)
            {
                return null;
            }

            var symbol = model.GetSymbolInfo(node).Symbol ?? model.GetDeclaredSymbol(node);
            if (symbol == null)
            {
                return null;
            }

            var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
            if (location == null)
            {
                var metadata = (symbol as IMethodSymbol)?.ContainingType?.Name
                    ?? (symbol as INamedTypeSymbol)?.Name
                    ?? symbol.ContainingAssembly?.Name;
                return new SymbolLocation
                {
                    FilePath = null,
                    Description = "Defined in metadata: " + (metadata ?? symbol.Name)
                };
            }

            var lineSpan = location.GetLineSpan();
            return new SymbolLocation
            {
                FilePath = location.SourceTree.FilePath,
                Line = lineSpan.StartLinePosition.Line + 1,
                Column = lineSpan.StartLinePosition.Character + 1,
                Description = symbol.ToDisplayString()
            };
        }

        public async Task<List<SymbolLocation>> FindReferencesAsync(string filePath, int offset)
        {
            var document = WorkspaceManager.Instance.GetDocument(filePath);
            if (document == null)
            {
                return new List<SymbolLocation>();
            }

            var model = await document.GetSemanticModelAsync();
            var tree = await document.GetSyntaxTreeAsync();
            if (model == null || tree == null)
            {
                return new List<SymbolLocation>();
            }

            var root = await tree.GetRootAsync();
            var token = root.FindToken(offset);
            var node = token.Parent;
            if (node == null)
            {
                return new List<SymbolLocation>();
            }

            var symbol = model.GetSymbolInfo(node).Symbol ?? model.GetDeclaredSymbol(node);
            if (symbol == null)
            {
                return new List<SymbolLocation>();
            }

            var results = new List<SymbolLocation>();
            try
            {
                var references = await SymbolFinder.FindReferencesAsync(symbol, document.Project.Solution);
                foreach (var referenced in references)
                {
                    foreach (var location in referenced.Locations)
                    {
                        var lineSpan = location.Location.GetLineSpan();
                        results.Add(new SymbolLocation
                        {
                            FilePath = location.Document.FilePath,
                            Line = lineSpan.StartLinePosition.Line + 1,
                            Column = lineSpan.StartLinePosition.Character + 1,
                            Description = referenced.Definition != null ? referenced.Definition.ToDisplayString() : document.Name
                        });
                    }
                }
            }
            catch
            {
            }
            return results;
        }
    }
}
