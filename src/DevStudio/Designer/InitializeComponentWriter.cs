using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

            var newText = tree.GetText().WithChanges(new TextChange(new TextSpan(spanStart, spanEnd - spanStart), sb.ToString()));
            return newText.ToString();
        }

        private static List<string> GenerateBody(DesignerModel model)
        {
            var lines = new List<string>();

            foreach (var control in model.Controls.Values.Where(c => !c.IsRoot))
            {
                lines.Add("this." + control.Name + " = new " + control.TypeName + "();");
            }

            foreach (var control in model.Controls.Values.Where(c => !c.IsRoot))
            {
                lines.Add("//");
                lines.Add("// " + control.Name);
                lines.Add("//");
                foreach (var property in control.Properties.Entries)
                {
                    lines.Add("this." + control.Name + "." + property.Key + " = " + property.Value + ";");
                }
            }

            lines.Add("//");
            lines.Add("// " + model.RootName);
            lines.Add("//");

            foreach (var control in model.Controls.Values.Where(c => !c.IsRoot))
            {
                if (!model.Controls.Values.Any(p => p.Children.Contains(control.Name)))
                {
                    lines.Add("this.Controls.Add(this." + control.Name + ");");
                }
            }
            foreach (var parent in model.Controls.Values)
            {
                foreach (var child in parent.Children)
                {
                    lines.Add(parent.Name + ".Controls.Add(this." + child + ");");
                }
            }

            foreach (var property in model.Root.Properties.Entries)
            {
                lines.Add("this." + property.Key + " = " + property.Value + ";");
            }

            lines.Add("this.ResumeLayout(false);");
            lines.Add("this.PerformLayout();");
            return lines;
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
                        expr, @"new\s+(System\.Drawing\.)?Font\s*\(\s*""(?<name>[^""]+)""\s*,\s*(?<size>[\d.]+)");
                    if (match.Success)
                    {
                        return new Font(match.Groups["name"].Value, float.Parse(match.Groups["size"].Value));
                    }
                }

                if (targetType == typeof(Color))
                {
                    var named = System.Text.RegularExpressions.Regex.Match(expr, @"Color\.(?<name>[A-Za-z]+)");
                    if (named.Success)
                    {
                        return Color.FromName(named.Groups["name"].Value);
                    }
                    var argb = System.Text.RegularExpressions.Regex.Match(
                        expr, @"FromArgb\s*\(\s*(?<a>\d+)\s*,\s*(?<r>\d+)\s*,\s*(?<g>\d+)\s*,\s*(?<b>\d+)\s*\)");
                    if (argb.Success)
                    {
                        return Color.FromArgb(int.Parse(argb.Groups["a"].Value), int.Parse(argb.Groups["r"].Value),
                            int.Parse(argb.Groups["g"].Value), int.Parse(argb.Groups["b"].Value));
                    }
                }

                if (targetType != null && targetType.IsEnum)
                {
                    var parts = expr.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p =>
                        {
                            var name = p.Trim();
                            var dot = name.LastIndexOf('.');
                            return dot >= 0 ? name.Substring(dot + 1) : name;
                        });
                    object combined = null;
                    foreach (var part in parts)
                    {
                        var value = Enum.Parse(targetType, part.Trim());
                        combined = combined == null ? value : Enum.ToObject(targetType, Convert.ToInt64(combined) | Convert.ToInt64(value));
                    }
                    return combined;
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
                        return expr.Substring(1, expr.Length - 2);
                    }
                    return expr;
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
                return targetType.FullName + "." + value;
            }
            if (value is bool)
            {
                return value.ToString().ToLowerInvariant();
            }
            if (value is string)
            {
                return "\"" + value + "\"";
            }
            if (value is float)
            {
                return value + "F";
            }
            return value.ToString();
        }
    }
}
