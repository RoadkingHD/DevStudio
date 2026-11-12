using System;
using System.Collections.Generic;

namespace DevStudio.Build
{
    public enum DiagnosticSeverityEx
    {
        Error,
        Warning,
        Message
    }

    public sealed class DiagnosticItem
    {
        public DiagnosticSeverityEx Severity { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Project { get; set; }

        public override string ToString()
        {
            var where = string.IsNullOrEmpty(File) ? "" : File + (Line > 0 ? "(" + Line + "," + Column + ")" : "") + ": ";
            return where + Severity.ToString().ToLowerInvariant() + " " + Code + ": " + Message;
        }
    }

    public sealed class BuildResult
    {
        public bool Success { get; set; }
        public string OutputPath { get; set; }
        public List<DiagnosticItem> Diagnostics { get; } = new List<DiagnosticItem>();
        public TimeSpan Duration { get; set; }
    }
}
