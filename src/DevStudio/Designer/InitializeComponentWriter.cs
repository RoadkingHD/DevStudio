using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DevStudio.Designer
{
    /// <summary>Applies a designer model back to the InitializeComponent method in source code.</summary>
    public static class InitializeComponentWriter
    {
        public static string Apply(string sourceText, DesignerModel model)
        {
            if (model == null || !model.Valid)
            {
                return sourceText;
            }

            SyntaxTree tree = null;
            CompilationUnitSyntax root = null;
            MethodDeclarationSyntax method = null;
            try
            {
                tree = CSharpSyntaxTree.ParseText(sourceText);
                root = tree.GetCompilationUnitRoot();
                method = root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.ValueText == "InitializeComponent");
            }
            catch
            {
                return sourceText;
            }
            if (method == null || method.Body == null)
            {
                return sourceText;
            }

            var bodyLines = GenerateBody(model);
            var spanStart = method.Body.OpenBraceToken.Span.End;
            var spanEnd = method.Body.CloseBraceToken.SpanStart;

            var indent = "        ";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine();
            foreach (var line in bodyLines)
            {
                sb.AppendLine(line.Length > 0 ? indent + line : "");
            }
            sb.Append(indent);

            var text = tree.GetText();
            var changes = new List<TextChange>
            {
                new TextChange(new TextSpan(spanStart, spanEnd - spanStart), sb.ToString())
            };

            var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl != null)
            {
                CollectFieldChanges(classDecl, model, changes);
            }

            changes.Sort((a, b) => b.Span.Start.CompareTo(a.Span.Start));
            var result = text;
            foreach (var change in changes)
            {
                result = result.WithChanges(change);
            }
            return result.ToString();
        }

        private static void CollectFieldChanges(ClassDeclarationSyntax classDecl, DesignerModel model, List<TextChange> changes)
        {
            var modelNames = new HashSet<string>(
                model.Controls.Values.Where(c => !c.IsRoot).Select(c => c.Name), StringComparer.Ordinal);

            var fields = classDecl.Members.OfType<FieldDeclarationSyntax>()
                .Where(f => f.Declaration != null)
                .ToList();

            foreach (var field in fields)
            {
                var typeText = field.Declaration.Type.ToString();
                if (!typeText.StartsWith("System.Windows.Forms.", StringComparison.Ordinal))
                {
                    continue;
                }
                bool stale = field.Declaration.Variables.Any(v => !modelNames.Contains(v.Identifier.ValueText));
                if (stale)
                {
                    changes.Add(new TextChange(field.FullSpan, string.Empty));
                }
            }

            var existingNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    existingNames.Add(variable.Identifier.ValueText);
                }
            }
            var missing = model.Controls.Values
                .Where(c => !c.IsRoot && !existingNames.Contains(c.Name))
                .ToList();
            if (missing.Count > 0)
            {
                var memberIndent = GetMemberIndent(classDecl);
                var sb = new System.Text.StringBuilder();
                foreach (var control in missing)
                {
                    sb.Append(memberIndent)
                        .Append("private ")
                        .Append(control.TypeName)
                        .Append(' ')
                        .Append(control.Name)
                        .Append(";\r\n");
                }
                changes.Add(new TextChange(new TextSpan(classDecl.CloseBraceToken.FullSpan.Start, 0), sb.ToString()));
            }
        }

        private static string GetMemberIndent(ClassDeclarationSyntax classDecl)
        {
            foreach (var member in classDecl.Members)
            {
                var lineText = classDecl.SyntaxTree.GetText().Lines
                    .GetLineFromPosition(member.SpanStart).ToString();
                int index = 0;
                while (index < lineText.Length && (lineText[index] == ' ' || lineText[index] == '\t'))
                {
                    index++;
                }
                return lineText.Substring(0, index);
            }
            return "        ";
        }

        private static List<string> GenerateBody(DesignerModel model)
        {
            var lines = new List<string>();
            lines.Add("this.SuspendLayout();");

            foreach (var control in model.Controls.Values.Where(c => !c.IsRoot))
            {
                lines.Add("this." + control.Name + " = new " + control.TypeName + "();");
            }

            foreach (var control in model.Controls.Values.Where(c => !c.IsRoot))
            {
                lines.Add("");
                lines.Add("//");
                lines.Add("// " + control.Name);
                lines.Add("//");
                foreach (var property in control.Properties.Entries)
                {
                    lines.Add("this." + control.Name + "." + property.Key + " = " + property.Value + ";");
                }
                if (control.Items.Count > 0)
                {
                    lines.Add("this." + control.Name + ".Items.AddRange(new object[] { " + string.Join(", ", control.Items) + " });");
                }
                foreach (var evt in control.Events.Entries)
                {
                    lines.Add("this." + control.Name + "." + evt.Key + " += " + evt.Value + ";");
                }
            }

            lines.Add("");
            lines.Add("//");
            lines.Add("// " + model.RootName);
            lines.Add("//");

            foreach (var control in model.Controls.Values.Where(c => !c.IsRoot))
            {
                if (IsVisualControl(control.TypeName) && !model.Controls.Values.Any(p => p.Children.Contains(control.Name)))
                {
                    lines.Add("this.Controls.Add(this." + control.Name + ");");
                }
            }
            foreach (var parent in model.Controls.Values)
            {
                foreach (var child in parent.Children)
                {
                    if (IsVisualControl(model.Controls[child].TypeName))
                    {
                        lines.Add(parent.Name + ".Controls.Add(this." + child + ");");
                    }
                }
            }

            foreach (var property in model.Root.Properties.Entries)
            {
                lines.Add("this." + property.Key + " = " + property.Value + ";");
            }
            foreach (var evt in model.Root.Events.Entries)
            {
                lines.Add("this." + evt.Key + " += " + evt.Value + ";");
            }

            foreach (var extra in model.ExtraStatements)
            {
                lines.Add(extra);
            }

            lines.Add("this.ResumeLayout(false);");
            lines.Add("this.PerformLayout();");
            return lines;
        }

        private static bool IsVisualControl(string typeName)
        {
            var type = Type.GetType(typeName + ", System.Windows.Forms");
            if (type == null)
            {
                type = Type.GetType(typeName);
            }
            if (type != null)
            {
                for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
                {
                    if (baseType.FullName == "System.Windows.Forms.Control")
                    {
                        return true;
                    }
                }
                return false;
            }
            switch (typeName)
            {
                case "System.Windows.Forms.ToolStripMenuItem":
                case "System.Windows.Forms.ToolStripSeparator":
                case "System.Windows.Forms.ToolStripTextBox":
                case "System.Windows.Forms.ToolStripComboBox":
                case "System.Windows.Forms.ToolStripLabel":
                case "System.Windows.Forms.ToolStripButton":
                case "System.Windows.Forms.ToolStripDropDownButton":
                case "System.Windows.Forms.ToolStripSplitButton":
                case "System.Windows.Forms.ToolStripDropDownMenu":
                case "System.Windows.Forms.BindingSource":
                case "System.Windows.Forms.Timer":
                case "System.Windows.Forms.ImageList":
                case "System.Windows.Forms.NotifyIcon":
                case "System.ComponentModel.IContainer":
                case "System.ComponentModel.Component":
                    return false;
            }
            return true;
        }

        public static string EnsureEventHandlerMethod(string sourceText, string handlerName, string className = null)
        {
            if (string.IsNullOrEmpty(sourceText) || string.IsNullOrEmpty(handlerName))
            {
                return sourceText;
            }
            SyntaxTree tree;
            CompilationUnitSyntax root;
            try
            {
                tree = CSharpSyntaxTree.ParseText(sourceText);
                root = tree.GetCompilationUnitRoot();
            }
            catch
            {
                return sourceText;
            }
            if (root.DescendantNodes().OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.ValueText == handlerName))
            {
                return sourceText;
            }
            ClassDeclarationSyntax classDecl = null;
            if (!string.IsNullOrEmpty(className))
            {
                classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .LastOrDefault(c => c.Identifier.ValueText == className);
            }
            if (classDecl == null)
            {
                var initMethod = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.ValueText == "InitializeComponent");
                if (initMethod == null)
                {
                    return sourceText;
                }
                classDecl = initMethod.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (classDecl == null)
                {
                    return sourceText;
                }
            }
            var pad = new string(' ', classDecl.Parent is NamespaceDeclarationSyntax ? 8 : 4);
            var lastToken = classDecl.CloseBraceToken.GetPreviousToken();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(pad).AppendLine("private void " + handlerName + "(object sender, System.EventArgs e)");
            sb.Append(pad).AppendLine("{");
            sb.Append(pad).AppendLine("    ");
            sb.Append(pad).Append("}");
            var newText = tree.GetText().WithChanges(new TextChange(new TextSpan(lastToken.Span.End, 0), sb.ToString()));
            return newText.ToString();
        }
    }

    /// <summary>Evaluates designer property expressions (literals, Point/Size/Color/Font constructors,
    /// enum member access) into runtime values for a given target property type.</summary>
    public static class DesignerExpressionEvaluator
    {
        public static object Evaluate(string expression, Type targetType)
        {
            if (string.IsNullOrEmpty(expression))
            {
                return null;
            }
            var expr = expression.Trim().TrimEnd(';');
            try
            {
                if (targetType == typeof(Point) || targetType == typeof(Size))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        expr, @"new\s+(System\.Drawing\.)?(Point|Size)\s*\(\s*(?<x>[\d-]+)\s*,\s*(?<y>[\d-]+)\s*\)");
                    if (match.Success)
                    {
                        int x = int.Parse(match.Groups["x"].Value);
                        int y = int.Parse(match.Groups["y"].Value);
                        return targetType == typeof(Point) ? (object)new Point(x, y) : new Size(x, y);
                    }
                }

                if (targetType == typeof(Font))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        expr, @"new\s+(System\.Drawing\.)?Font\s*\(\s*""(?<name>[^""]+)""\s*,\s*(?<size>[\d.]+)\s*F?");
                    if (match.Success)
                    {
                        var style = FontStyle.Regular;
                        foreach (System.Text.RegularExpressions.Match styleMatch in
                            System.Text.RegularExpressions.Regex.Matches(expr, @"FontStyle\.(?<name>\w+)"))
                        {
                            FontStyle parsed;
                            if (Enum.TryParse(styleMatch.Groups["name"].Value, out parsed))
                            {
                                style |= parsed;
                            }
                        }
                        return new Font(match.Groups["name"].Value,
                            float.Parse(match.Groups["size"].Value, System.Globalization.CultureInfo.InvariantCulture), style);
                    }
                }

                if (targetType != null && targetType.FullName == "System.Windows.Forms.Padding")
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        expr, @"new\s+(System\.Windows\.Forms\.)?Padding\s*\((?<args>[^()]*)\)");
                    if (match.Success)
                    {
                        var numbers = System.Text.RegularExpressions.Regex.Matches(match.Groups["args"].Value, @"-?\d+")
                            .Cast<System.Text.RegularExpressions.Match>()
                            .Select(m => int.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture))
                            .ToList();
                        if (numbers.Count >= 4)
                        {
                            return Activator.CreateInstance(targetType, numbers[0], numbers[1], numbers[2], numbers[3]);
                        }
                        if (numbers.Count == 1)
                        {
                            return Activator.CreateInstance(targetType, numbers[0]);
                        }
                    }
                }

                if (targetType == typeof(SizeF))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        expr, @"new\s+(System\.Drawing\.)?SizeF?\s*\(\s*(?<w>-?[\d.]+)\s*F?\s*,\s*(?<h>-?[\d.]+)\s*F?\s*\)");
                    if (match.Success)
                    {
                        return new SizeF(
                            float.Parse(match.Groups["w"].Value, System.Globalization.CultureInfo.InvariantCulture),
                            float.Parse(match.Groups["h"].Value, System.Globalization.CultureInfo.InvariantCulture));
                    }
                }

                if (targetType == typeof(DateTime))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        expr, @"new\s+(System\.)?DateTime\s*\((?<args>[^()]*)\)");
                    if (match.Success)
                    {
                        var numbers = System.Text.RegularExpressions.Regex.Matches(match.Groups["args"].Value, @"-?\d+")
                            .Cast<System.Text.RegularExpressions.Match>()
                            .Select(m => long.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture))
                            .ToList();
                        if (numbers.Count == 1)
                        {
                            return new DateTime(numbers[0]);
                        }
                        if (numbers.Count >= 3)
                        {
                            return new DateTime(
                                (int)numbers[0], (int)numbers[1], (int)numbers[2],
                                numbers.Count >= 4 ? (int)numbers[3] : 0,
                                numbers.Count >= 5 ? (int)numbers[4] : 0,
                                numbers.Count >= 6 ? (int)numbers[5] : 0,
                                numbers.Count >= 7 ? (int)numbers[6] : 0);
                        }
                    }
                }

                if (targetType == typeof(Color))
                {
                    var argbIndex = expr.IndexOf("FromArgb", StringComparison.Ordinal);
                    if (argbIndex >= 0)
                    {
                        var open = expr.IndexOf('(', argbIndex);
                        if (open >= 0)
                        {
                            int depth = 0, end = -1;
                            for (int i = open; i < expr.Length; i++)
                            {
                                if (expr[i] == '(')
                                {
                                    depth++;
                                }
                                else if (expr[i] == ')')
                                {
                                    depth--;
                                    if (depth == 0)
                                    {
                                        end = i;
                                        break;
                                    }
                                }
                            }
                            if (end > open)
                            {
                                var inner = expr.Substring(open + 1, end - open - 1);
                                var numbers = System.Text.RegularExpressions.Regex.Matches(inner, @"\d+")
                                    .Cast<System.Text.RegularExpressions.Match>()
                                    .Select(m => int.Parse(m.Value))
                                    .ToList();
                                if (numbers.Count >= 4)
                                {
                                    return Color.FromArgb(numbers[0], numbers[1], numbers[2], numbers[3]);
                                }
                                if (numbers.Count == 3)
                                {
                                    return Color.FromArgb(numbers[0], numbers[1], numbers[2]);
                                }
                            }
                        }
                    }
                    var sys = System.Text.RegularExpressions.Regex.Match(expr, @"SystemColors\.(?<name>[A-Za-z]\w*)");
                    if (sys.Success)
                    {
                        var property = typeof(SystemColors).GetProperty(sys.Groups["name"].Value,
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        if (property != null)
                        {
                            return (Color)property.GetValue(null, null);
                        }
                    }
                    var named = System.Text.RegularExpressions.Regex.Match(expr, @"Color\.(?<name>[A-Za-z]\w*)");
                    if (named.Success && named.Groups["name"].Value != "FromArgb")
                    {
                        return Color.FromName(named.Groups["name"].Value);
                    }
                }

                if (targetType != null && targetType.IsEnum)
                {
                    object combined = null;
                    foreach (System.Text.RegularExpressions.Match m in
                        System.Text.RegularExpressions.Regex.Matches(expr, @"[A-Za-z_]\w*"))
                    {
                        object value;
                        try
                        {
                            value = Enum.Parse(targetType, m.Value, true);
                        }
                        catch
                        {
                            continue;
                        }
                        combined = combined == null ? value : Enum.ToObject(targetType, Convert.ToInt64(combined) | Convert.ToInt64(value));
                    }
                    if (combined != null)
                    {
                        return combined;
                    }
                }

                if (targetType == typeof(bool))
                {
                    return bool.Parse(expr);
                }
                if (targetType == typeof(int))
                {
                    return int.Parse(expr);
                }
                if (targetType == typeof(string))
                {
                    if (expr.StartsWith("\"") && expr.EndsWith("\"") && expr.Length >= 2)
                    {
                        return UnescapeSourceString(expr.Substring(1, expr.Length - 2));
                    }
                    return expr;
                }

                if (targetType == typeof(char))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(expr, @"'(?<ch>(?:[^'\\]|\\.))'");
                    if (match.Success)
                    {
                        var text = UnescapeSourceString(match.Groups["ch"].Value);
                        if (text.Length == 1)
                        {
                            return text[0];
                        }
                    }
                }

                if (targetType == typeof(string[]))
                {
                    var openBrace = expr.IndexOf('{');
                    var closeBrace = expr.LastIndexOf('}');
                    if (openBrace < 0 || closeBrace <= openBrace)
                    {
                        return new string[0];
                    }
                    var inner = expr.Substring(openBrace + 1, closeBrace - openBrace - 1);
                    var values = new List<string>();
                    foreach (System.Text.RegularExpressions.Match m in
                        System.Text.RegularExpressions.Regex.Matches(inner, "\"((?:[^\"\\\\]|\\\\.)*)\""))
                    {
                        values.Add(UnescapeSourceString(m.Groups[1].Value));
                    }
                    return values.ToArray();
                }

                if (targetType != null)
                {
                    var converter = System.ComponentModel.TypeDescriptor.GetConverter(targetType);
                    if (converter != null && converter.CanConvertFrom(typeof(string)))
                    {
                        var plain = expr;
                        if (plain.StartsWith("new ") && plain.EndsWith(")"))
                        {
                            var open = plain.IndexOf('(');
                            if (open >= 0)
                            {
                                plain = plain.Substring(open + 1).TrimEnd(')');
                            }
                        }
                        if (targetType.IsEnum)
                        {
                            var dot = plain.LastIndexOf('.');
                            if (dot >= 0)
                            {
                                plain = plain.Substring(dot + 1);
                            }
                        }
                        return converter.ConvertFromString(plain);
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public static string ToSourceExpression(object value, Type targetType)
        {
            if (value == null)
            {
                return null;
            }
            if (value is Point)
            {
                var p = (Point)value;
                return "new System.Drawing.Point(" + p.X + ", " + p.Y + ")";
            }
            if (value is Size)
            {
                var s = (Size)value;
                return "new System.Drawing.Size(" + s.Width + ", " + s.Height + ")";
            }
            if (value is Font)
            {
                var f = (Font)value;
                return "new System.Drawing.Font(\"" + f.Name + "\", " + f.Size.ToString(System.Globalization.CultureInfo.InvariantCulture) + "F)";
            }
            if (value is Color)
            {
                var c = (Color)value;
                if (c.IsNamedColor)
                {
                    return "System.Drawing.Color." + c.Name;
                }
                return "System.Drawing.Color.FromArgb(" + c.A + ", " + c.R + ", " + c.G + ", " + c.B + ")";
            }
            if (targetType != null && targetType.IsEnum)
            {
                var names = value.ToString().Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                return string.Join(" | ", names.Select(n => targetType.FullName + "." + n.Trim()));
            }
            if (value is bool)
            {
                return value.ToString().ToLowerInvariant();
            }
            if (value is string)
            {
                return QuoteSourceString((string)value);
            }
            if (value is string[])
            {
                var strings = (string[])value;
                return "new string[] { " + string.Join(", ", strings.Select(QuoteSourceString)) + " }";
            }
            if (value is DateTime)
            {
                var dt = (DateTime)value;
                return "new System.DateTime(" + dt.Year + ", " + dt.Month + ", " + dt.Day + ", "
                    + dt.Hour + ", " + dt.Minute + ", " + dt.Second + ")";
            }
            if (value is char)
            {
                var ch = (char)value;
                if (ch == '\'' || ch == '\\')
                {
                    return null;
                }
                return "'" + ch + "'";
            }
            if (value is float)
            {
                return ((float)value).ToString(System.Globalization.CultureInfo.InvariantCulture) + "F";
            }
            if (value is decimal || value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong || value is double)
            {
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            if (value.GetType().FullName == "System.Windows.Forms.Padding")
            {
                var padType = value.GetType();
                var left = (int)padType.GetProperty("Left").GetValue(value, null);
                var top = (int)padType.GetProperty("Top").GetValue(value, null);
                var right = (int)padType.GetProperty("Right").GetValue(value, null);
                var bottom = (int)padType.GetProperty("Bottom").GetValue(value, null);
                return "new System.Windows.Forms.Padding(" + left + ", " + top + ", " + right + ", " + bottom + ")";
            }
            if (value is SizeF)
            {
                var sizeF = (SizeF)value;
                return "new System.Drawing.SizeF(" + sizeF.Width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "F, "
                    + sizeF.Height.ToString(System.Globalization.CultureInfo.InvariantCulture) + "F)";
            }
            if (value is PointF)
            {
                var pointF = (PointF)value;
                return "new System.Drawing.PointF(" + pointF.X.ToString(System.Globalization.CultureInfo.InvariantCulture) + "F, "
                    + pointF.Y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "F)";
            }
            if (value is Rectangle)
            {
                var rect = (Rectangle)value;
                return "new System.Drawing.Rectangle(" + rect.X + ", " + rect.Y + ", " + rect.Width + ", " + rect.Height + ")";
            }
            return null;
        }

        public static string QuoteSourceString(string value)
        {
            var sb = new StringBuilder("\"");
            foreach (var ch in value ?? "")
            {
                if (ch == '\\')
                {
                    sb.Append("\\\\");
                }
                else if (ch == '"')
                {
                    sb.Append("\\\"");
                }
                else if (ch == '\n')
                {
                    sb.Append("\\n");
                }
                else if (ch == '\r')
                {
                    sb.Append("\\r");
                }
                else if (ch == '\t')
                {
                    sb.Append("\\t");
                }
                else
                {
                    sb.Append(ch);
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }

        private static string UnescapeSourceString(string value)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length)
                {
                    i++;
                    switch (value[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(value[i]); break;
                    }
                }
                else
                {
                    sb.Append(value[i]);
                }
            }
            return sb.ToString();
        }
    }
}
