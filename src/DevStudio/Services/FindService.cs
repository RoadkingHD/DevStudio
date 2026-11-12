using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DevStudio.Services
{
    public sealed class FindHit
    {
        public string File { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string LineText { get; set; }
    }

    /// <summary>Plain text search across files.</summary>
    public static class FindService
    {
        public static List<FindHit> Search(IEnumerable<string> files, string query, bool matchCase, bool wholeWord, bool useRegex)
        {
            var hits = new List<FindHit>();
            if (string.IsNullOrEmpty(query))
            {
                return hits;
            }

            Regex regex;
            try
            {
                var pattern = useRegex
                    ? query
                    : Regex.Escape(query);
                if (wholeWord)
                {
                    pattern = @"\b" + pattern + @"\b";
                }
                regex = new Regex(pattern, matchCase ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));
            }
            catch (ArgumentException)
            {
                return hits;
            }

            foreach (var file in files)
            {
                string[] lines;
                try
                {
                    if (!File.Exists(file))
                    {
                        continue;
                    }
                    lines = File.ReadAllLines(file);
                }
                catch
                {
                    continue;
                }
                for (int i = 0; i < lines.Length; i++)
                {
                    var match = regex.Match(lines[i]);
                    while (match.Success)
                    {
                        hits.Add(new FindHit
                        {
                            File = file,
                            Line = i + 1,
                            Column = match.Index + 1,
                            LineText = lines[i].Trim()
                        });
                        if (hits.Count >= 1000)
                        {
                            return hits;
                        }
                        match = match.NextMatch();
                    }
                }
            }
            return hits;
        }
    }
}
