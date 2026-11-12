using System;
using System.Collections.Generic;
using System.IO;
using DevStudio.Projects;
using DevStudio.Theming;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace DevStudio.Editor
{
    /// <summary>State of one file opened in the editor.</summary>
    public sealed class EditorDocument
    {
        public string FilePath { get; }
        public DevLanguage Language { get; }
        public TextDocument Text { get; }
        public bool Dirty { get; set; }
        public HashSet<int> BreakpointLines { get; } = new HashSet<int>();
        public RoslynColorizer RoslynColorizer { get; }
        public CppColorizer CppColorizer { get; }
        public int CurrentDebugLine { get; set; } = -1;

        public string DisplayName
        {
            get { return Path.GetFileName(FilePath); }
        }

        public EditorDocument(string filePath, EditorTheme theme)
        {
            FilePath = filePath;
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            Language = ext == ".c" || ext == ".cpp" || ext == ".cc" || ext == ".cxx" || ext == ".h" || ext == ".hpp"
                ? DevLanguage.Cpp
                : DevLanguage.CSharp;

            string content = File.Exists(filePath) ? File.ReadAllText(filePath) : "";
            Text = new TextDocument(content);
            Text.TextChanged += (s, e) => Dirty = true;

            if (Language == DevLanguage.CSharp)
            {
                RoslynColorizer = new RoslynColorizer(this, theme);
            }
            else
            {
                CppColorizer = new CppColorizer(this, theme);
            }
        }

        public IVisualLineTransformer GetColorizer()
        {
            return (IVisualLineTransformer)RoslynColorizer ?? (IVisualLineTransformer)CppColorizer;
        }
    }
}
