using System.Collections.Generic;
using System.Drawing;

namespace DevStudio.Theming
{
    /// <summary>A color scheme for the code editor (and the surrounding UI).</summary>
    public sealed class EditorTheme
    {
        public string Name { get; }

        public Color Background { get; }
        public Color Foreground { get; }
        public Color Selection { get; }
        public Color CurrentLine { get; }
        public Color LineNumber { get; }
        public Color LineNumberActive { get; }
        public Color BreakpointMark { get; }
        public Color CurrentStatement { get; }
        public Color Keyword { get; }
        public Color ControlKeyword { get; }
        public Color Comment { get; }
        public Color String { get; }
        public Color Number { get; }
        public Color Type { get; }
        public Color Identifier { get; }
        public Color Preprocessor { get; }
        public Color Punctuation { get; }
        public Color Operator { get; }
        public Color InterfaceName { get; }
        public Color MethodName { get; }
        public Color FieldName { get; }
        public Color VariableName { get; }
        public Color XmlDoc { get; }
        public Color MinimapViewport { get; }
        public bool IsDark { get; }

        public EditorTheme(string name, bool dark,
            Color background, Color foreground, Color selection, Color currentLine,
            Color lineNumber, Color lineNumberActive,
            Color keyword, Color controlKeyword, Color comment, Color str, Color number,
            Color type, Color identifier, Color preprocessor, Color punctuation, Color op,
            Color interfaceName, Color methodName, Color fieldName, Color variableName,
            Color xmlDoc, Color minimapViewport)
        {
            Name = name;
            IsDark = dark;
            Background = background;
            Foreground = foreground;
            Selection = selection;
            CurrentLine = currentLine;
            LineNumber = lineNumber;
            LineNumberActive = lineNumberActive;
            BreakpointMark = Color.FromArgb(232, 17, 35);
            CurrentStatement = Color.FromArgb(255, 214, 0);
            Keyword = keyword;
            ControlKeyword = controlKeyword;
            Comment = comment;
            String = str;
            Number = number;
            Type = type;
            Identifier = identifier;
            Preprocessor = preprocessor;
            Punctuation = punctuation;
            Operator = op;
            InterfaceName = interfaceName;
            MethodName = methodName;
            FieldName = fieldName;
            VariableName = variableName;
            XmlDoc = xmlDoc;
            MinimapViewport = minimapViewport;
        }

        public Color Panel
        {
            get { return IsDark ? Color.FromArgb(45, 45, 48) : Color.White; }
        }

        public Color Text
        {
            get { return Foreground; }
        }

        public Color Accent
        {
            get { return IsDark ? Color.FromArgb(0, 122, 204) : Color.FromArgb(0, 120, 215); }
        }

        public static readonly EditorTheme Dark =
            new EditorTheme("Dark", true,
                Color.FromArgb(30, 30, 30), Color.FromArgb(212, 212, 212), Color.FromArgb(38, 79, 120), Color.FromArgb(37, 37, 38),
                Color.FromArgb(133, 133, 133), Color.FromArgb(198, 198, 198),
                Color.FromArgb(86, 156, 214), Color.FromArgb(204, 154, 231), Color.FromArgb(106, 153, 85), Color.FromArgb(206, 145, 120), Color.FromArgb(181, 206, 168),
                Color.FromArgb(78, 201, 176), Color.FromArgb(156, 220, 254), Color.FromArgb(155, 155, 155), Color.FromArgb(212, 212, 212), Color.FromArgb(212, 212, 212),
                Color.FromArgb(184, 215, 163), Color.FromArgb(220, 220, 170), Color.FromArgb(156, 220, 254), Color.FromArgb(156, 220, 254),
                Color.FromArgb(96, 139, 78), Color.FromArgb(60, 60, 60));

        public static readonly EditorTheme Light =
            new EditorTheme("Light", false,
                Color.White, Color.FromArgb(30, 30, 30), Color.FromArgb(173, 214, 255), Color.FromArgb(245, 245, 245),
                Color.FromArgb(35, 110, 170), Color.FromArgb(0, 0, 0),
                Color.FromArgb(0, 0, 255), Color.FromArgb(175, 0, 219), Color.FromArgb(0, 128, 0), Color.FromArgb(163, 21, 21), Color.FromArgb(9, 134, 88),
                Color.FromArgb(38, 127, 153), Color.FromArgb(0, 0, 0), Color.FromArgb(128, 128, 128), Color.FromArgb(30, 30, 30), Color.FromArgb(30, 30, 30),
                Color.FromArgb(43, 145, 175), Color.FromArgb(121, 94, 38), Color.FromArgb(0, 16, 128), Color.FromArgb(0, 16, 128),
                Color.FromArgb(0, 128, 0), Color.FromArgb(220, 220, 220));

        public static readonly EditorTheme Monokai =
            new EditorTheme("Monokai", true,
                Color.FromArgb(39, 40, 34), Color.FromArgb(248, 248, 242), Color.FromArgb(73, 72, 62), Color.FromArgb(62, 61, 50),
                Color.FromArgb(117, 113, 94), Color.FromArgb(248, 248, 242),
                Color.FromArgb(249, 38, 114), Color.FromArgb(249, 38, 114), Color.FromArgb(117, 113, 94), Color.FromArgb(230, 219, 116), Color.FromArgb(174, 129, 255),
                Color.FromArgb(102, 217, 239), Color.FromArgb(248, 248, 242), Color.FromArgb(249, 38, 114), Color.FromArgb(248, 248, 242), Color.FromArgb(249, 38, 114),
                Color.FromArgb(166, 226, 46), Color.FromArgb(166, 226, 46), Color.FromArgb(248, 248, 242), Color.FromArgb(253, 151, 31),
                Color.FromArgb(117, 113, 94), Color.FromArgb(62, 61, 50));

        public static readonly EditorTheme Dracula =
            new EditorTheme("Dracula", true,
                Color.FromArgb(40, 42, 54), Color.FromArgb(248, 248, 242), Color.FromArgb(68, 71, 90), Color.FromArgb(52, 55, 70),
                Color.FromArgb(98, 114, 164), Color.FromArgb(248, 248, 242),
                Color.FromArgb(255, 121, 198), Color.FromArgb(255, 121, 198), Color.FromArgb(98, 114, 164), Color.FromArgb(241, 250, 140), Color.FromArgb(189, 147, 249),
                Color.FromArgb(139, 233, 253), Color.FromArgb(248, 248, 242), Color.FromArgb(255, 121, 198), Color.FromArgb(248, 248, 242), Color.FromArgb(255, 121, 198),
                Color.FromArgb(139, 233, 253), Color.FromArgb(80, 250, 123), Color.FromArgb(139, 233, 253), Color.FromArgb(248, 248, 242),
                Color.FromArgb(98, 114, 164), Color.FromArgb(59, 61, 77));

        public static readonly EditorTheme OneDark =
            new EditorTheme("One Dark", true,
                Color.FromArgb(40, 44, 52), Color.FromArgb(171, 178, 191), Color.FromArgb(62, 68, 81), Color.FromArgb(44, 49, 60),
                Color.FromArgb(92, 99, 112), Color.FromArgb(171, 178, 191),
                Color.FromArgb(198, 120, 221), Color.FromArgb(198, 120, 221), Color.FromArgb(92, 99, 112), Color.FromArgb(152, 195, 121), Color.FromArgb(209, 154, 102),
                Color.FromArgb(229, 192, 123), Color.FromArgb(171, 178, 191), Color.FromArgb(198, 120, 221), Color.FromArgb(171, 178, 191), Color.FromArgb(86, 182, 194),
                Color.FromArgb(229, 192, 123), Color.FromArgb(97, 175, 239), Color.FromArgb(224, 108, 117), Color.FromArgb(224, 108, 117),
                Color.FromArgb(92, 99, 112), Color.FromArgb(58, 63, 75));

        public static readonly EditorTheme SolarizedDark =
            new EditorTheme("Solarized Dark", true,
                Color.FromArgb(0, 43, 54), Color.FromArgb(131, 148, 150), Color.FromArgb(7, 54, 66), Color.FromArgb(0, 50, 61),
                Color.FromArgb(88, 110, 117), Color.FromArgb(147, 161, 161),
                Color.FromArgb(133, 153, 0), Color.FromArgb(203, 75, 22), Color.FromArgb(88, 110, 117), Color.FromArgb(42, 161, 152), Color.FromArgb(42, 161, 152),
                Color.FromArgb(38, 139, 210), Color.FromArgb(147, 161, 161), Color.FromArgb(203, 75, 22), Color.FromArgb(147, 161, 161), Color.FromArgb(147, 161, 161),
                Color.FromArgb(181, 137, 0), Color.FromArgb(181, 137, 0), Color.FromArgb(38, 139, 210), Color.FromArgb(38, 139, 210),
                Color.FromArgb(88, 110, 117), Color.FromArgb(7, 54, 66));

        public static readonly IReadOnlyList<EditorTheme> All = new[]
        {
            Dark, Light, Monokai, Dracula, OneDark, SolarizedDark
        };

        public override string ToString()
        {
            return Name;
        }
    }
}
