using System;
using System.Collections.Generic;
using System.IO;
using DevStudio.Projects;

namespace DevStudio.Editor
{
    /// <summary>State of one file opened in the editor.</summary>
    public sealed class EditorDocument
    {
        public string FilePath { get; }
        public DevLanguage Language { get; }
        public string Text { get; set; }
        public bool Dirty { get; set; }
        public HashSet<int> BreakpointLines { get; } = new HashSet<int>();
        public int CurrentDebugLine { get; set; } = -1;

        public string DisplayName
        {
            get { return Path.GetFileName(FilePath); }
        }

        public EditorDocument(string filePath)
        {
            FilePath = filePath;
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            Language = ext == ".c" || ext == ".cpp" || ext == ".cc" || ext == ".cxx" || ext == ".h" || ext == ".hpp"
                ? DevLanguage.Cpp
                : DevLanguage.CSharp;

            Text = File.Exists(filePath) ? File.ReadAllText(filePath) : "";
        }

        public void GetLocation(int offset, out int line, out int column)
        {
            line = 1;
            column = 1;
            if (offset <= 0 || string.IsNullOrEmpty(Text))
            {
                return;
            }
            if (offset > Text.Length)
            {
                offset = Text.Length;
            }
            for (int i = 0; i < offset; i++)
            {
                if (Text[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }
        }

        public int GetLineOffset(int lineNumber)
        {
            if (lineNumber <= 1 || string.IsNullOrEmpty(Text))
            {
                return 0;
            }
            int line = 1;
            for (int i = 0; i < Text.Length; i++)
            {
                if (Text[i] == '\n')
                {
                    line++;
                    if (line == lineNumber)
                    {
                        return i + 1;
                    }
                }
            }
            return Text.Length;
        }

        public int GetLineLength(int lineNumber)
        {
            int start = GetLineOffset(lineNumber);
            int end = start;
            while (end < Text.Length && Text[end] != '\n' && Text[end] != '\r')
            {
                end++;
            }
            return end - start;
        }

        public int LineCount
        {
            get
            {
                if (string.IsNullOrEmpty(Text))
                {
                    return 1;
                }
                int count = 1;
                for (int i = 0; i < Text.Length; i++)
                {
                    if (Text[i] == '\n')
                    {
                        count++;
                    }
                }
                return count;
            }
        }
    }
}
