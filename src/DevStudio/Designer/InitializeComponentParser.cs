using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DevStudio.Designer
{
    public sealed class ControlInfo
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public bool IsRoot { get; set; }
        public OrderedProperties Properties { get; } = new OrderedProperties();
        public OrderedProperties Events { get; } = new OrderedProperties();
        public List<string> Children { get; } = new List<string>();
    }

    /// <summary>Property bag that remembers the original source expressions and their order.</summary>
    public sealed class OrderedProperties
    {
        private readonly List<KeyValuePair<string, string>> _entries = new List<KeyValuePair<string, string>>();

        public string this[string name]
        {
            get
            {
                var entry = _entries.FirstOrDefault(kv => kv.Key == name);
                return entry.Key == null ? null : entry.Value;
            }
            set
            {
                int index = _entries.FindIndex(kv => kv.Key == name);
                if (index >= 0)
                {
                    _entries[index] = new KeyValuePair<string, string>(name, value);
                }
                else
                {
                    _entries.Add(new KeyValuePair<string, string>(name, value));
                }
            }
        }

        public IEnumerable<KeyValuePair<string, string>> Entries
        {
            get { return _entries; }
        }

        public void Remove(string name)
        {
            _entries.RemoveAll(kv => kv.Key == name);
        }

        public bool Contains(string name)
        {
            return _entries.Any(kv => kv.Key == name);
        }
    }

    public sealed class DesignerModel
    {
        public string RootName { get; set; } = "this";
        public string RootTypeName { get; set; } = "System.Windows.Forms.Form";
        public ControlInfo Root { get; set; }
        public Dictionary<string, ControlInfo> Controls { get; } = new Dictionary<string, ControlInfo>(StringComparer.Ordinal);
        public List<string> ExtraStatements { get; } = new List<string>();
        public bool Valid { get; set; }
        public string ClassName { get; set; }
    }

    /// <summary>Parses the InitializeComponent method of a Form/Control source file into a designer model.</summary>
    public static class InitializeComponentParser
    {
        public static DesignerModel Parse(string sourceText)
        {
            var model = new DesignerModel();
            if (string.IsNullOrEmpty(sourceText) || sourceText.IndexOf("InitializeComponent", StringComparison.Ordinal) < 0)
            {
                return model;
            }

            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(sourceText);
            }
            catch
            {
                return model;
            }

            var root = tree.GetCompilationUnitRoot();
            var method = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == "InitializeComponent");
            if (method == null || method.Body == null)
            {
                return model;
            }

            var classDecl = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            model.ClassName = classDecl != null ? classDecl.Identifier.ValueText : null;
            if (classDecl != null && classDecl.BaseList != null)
            {
                var baseType = classDecl.BaseList.Types.FirstOrDefault();
                if (baseType != null)
                {
                    var baseName = baseType.Type.ToString();
                    if (baseName == "Form" || baseName.EndsWith(".Form"))
                    {
                        model.RootTypeName = "System.Windows.Forms.Form";
                    }
                    else if (baseName.Contains("UserControl") || baseName == "Control" || baseName.EndsWith(".UserControl"))
                    {
                        model.RootTypeName = "System.Windows.Forms.UserControl";
                    }
                }
            }

            var rootInfo = new ControlInfo
            {
                Name = "this",
                TypeName = model.RootTypeName,
                IsRoot = true
            };
            model.Root = rootInfo;
            model.Controls["this"] = rootInfo;

            foreach (var statement in method.Body.Statements)
            {
                try
                {
                    ProcessStatement(statement, model);
                }
                catch
                {
                }
            }

            model.Valid = true;
            return model;
        }

        private static void ProcessStatement(StatementSyntax statement, DesignerModel model)
        {
            var exprStatement = statement as ExpressionStatementSyntax;
            if (exprStatement != null)
            {
                var statementText = exprStatement.ToString();
                var expression = exprStatement.Expression;
                var assignment = expression as AssignmentExpressionSyntax;
                if (assignment != null)
                {
                    ProcessAssignment(assignment, statementText, model);
                    return;
                }
                var invocation = expression as InvocationExpressionSyntax;
                if (invocation != null)
                {
                    ProcessInvocation(invocation, statementText, model);
                    return;
                }
                model.ExtraStatements.Add(statementText);
            }
        }

        private static void ProcessAssignment(AssignmentExpressionSyntax assignment, string statementText, DesignerModel model)
        {
            var op = assignment.OperatorToken.ValueText;
            if (op != "=" && op != "+=")
            {
                return;
            }

            var target = assignment.Left;
            var valueText = assignment.Right.ToString();

            var identifier = target as IdentifierNameSyntax;
            if (identifier != null)
            {
                if (op == "+=")
                {
                    model.ExtraStatements.Add(statementText);
                    return;
                }
                var creation = assignment.Right as ObjectCreationExpressionSyntax;
                if (creation != null)
                {
                    var typeName = NormalizeType(creation.Type.ToString());
                    if (!typeName.StartsWith("System.ComponentModel."))
                    {
                        var info = GetOrCreate(model, identifier.Identifier.ValueText);
                        info.TypeName = typeName;
                    }
                }
                return;
            }

            var memberAccess = target as MemberAccessExpressionSyntax;
            if (memberAccess != null)
            {
                if (memberAccess.Expression is ThisExpressionSyntax && op == "=")
                {
                    var creation = assignment.Right as ObjectCreationExpressionSyntax;
                    if (creation != null)
                    {
                        var createdType = NormalizeType(creation.Type.ToString());
                        if (createdType.StartsWith("System.Drawing.") || createdType.StartsWith("System.ComponentModel."))
                        {
                            model.Root.Properties[memberAccess.Name.Identifier.ValueText] = valueText;
                        }
                        else
                        {
                            GetOrCreate(model, memberAccess.Name.Identifier.ValueText).TypeName = createdType;
                        }
                        return;
                    }
                }
                var ownerName = GetOwnerName(memberAccess.Expression);
                var propertyName = memberAccess.Name.Identifier.ValueText;
                if (ownerName == null || propertyName == "Controls" || propertyName == "DataBindings" || propertyName == "Items" || propertyName == "Columns")
                {
                    return;
                }
                ControlInfo info;
                if (!model.Controls.TryGetValue(ownerName, out info))
                {
                    model.ExtraStatements.Add(statementText);
                    return;
                }
                if (op == "+=")
                {
                    info.Events[propertyName] = valueText;
                    return;
                }
                info.Properties[propertyName] = valueText;
                return;
            }

            model.ExtraStatements.Add(statementText);
        }

        private static void ProcessInvocation(InvocationExpressionSyntax invocation, string statementText, DesignerModel model)
        {
            var called = invocation.Expression as MemberAccessExpressionSyntax;
            if (called == null)
            {
                model.ExtraStatements.Add(statementText);
                return;
            }
            var methodName = called.Name.Identifier.ValueText;
            if (methodName == "SuspendLayout" || methodName == "ResumeLayout" || methodName == "PerformLayout")
            {
                return;
            }
            if (methodName != "Add" && methodName != "AddRange")
            {
                model.ExtraStatements.Add(statementText);
                return;
            }
            if (!called.Expression.ToString().EndsWith("Controls"))
            {
                model.ExtraStatements.Add(statementText);
                return;
            }

            var containerExpression = called.Expression;
            var containerMember = containerExpression as MemberAccessExpressionSyntax;
            if (containerMember != null && containerMember.Name.Identifier.ValueText == "Controls")
            {
                containerExpression = containerMember.Expression;
            }
            var ownerName = GetOwnerName(containerExpression);
            if (ownerName == null || !model.Controls.ContainsKey(ownerName))
            {
                model.ExtraStatements.Add(statementText);
                return;
            }

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                var childName = GetOwnerName(argument.Expression);
                var identifier = argument.Expression as IdentifierNameSyntax;
                if (childName == null && identifier != null)
                {
                    childName = identifier.Identifier.ValueText;
                }
                if (childName != null && model.Controls.ContainsKey(childName) && childName != "this")
                {
                    if (!model.Controls[ownerName].Children.Contains(childName))
                    {
                        model.Controls[ownerName].Children.Add(childName);
                    }
                }
            }
        }

        private static ControlInfo GetOrCreate(DesignerModel model, string name)
        {
            ControlInfo info;
            if (!model.Controls.TryGetValue(name, out info))
            {
                info = new ControlInfo { Name = name };
                model.Controls[name] = info;
            }
            return info;
        }

        private static string GetOwnerName(ExpressionSyntax expression)
        {
            if (expression is ThisExpressionSyntax)
            {
                return "this";
            }
            var memberAccess = expression as MemberAccessExpressionSyntax;
            if (memberAccess == null)
            {
                return null;
            }
            if (IsThisRooted(memberAccess.Expression))
            {
                return memberAccess.Name.Identifier.ValueText;
            }
            var outer = GetOwnerName(memberAccess.Expression);
            if (outer != null)
            {
                return outer;
            }
            var identifier = memberAccess.Expression as IdentifierNameSyntax;
            return identifier != null ? identifier.Identifier.ValueText : memberAccess.Expression.ToString();
        }

        private static bool IsThisRooted(ExpressionSyntax expression)
        {
            if (expression is ThisExpressionSyntax)
            {
                return true;
            }
            var memberAccess = expression as MemberAccessExpressionSyntax;
            return memberAccess != null && IsThisRooted(memberAccess.Expression);
        }

        public static string NormalizeType(string typeName)
        {
            typeName = typeName.Trim();
            if (typeName.StartsWith("new "))
            {
                typeName = typeName.Substring(4);
            }
            if (!typeName.Contains("."))
            {
                switch (typeName)
                {
                    case "Form": return "System.Windows.Forms.Form";
                    case "Button": return "System.Windows.Forms.Button";
                    case "Label": return "System.Windows.Forms.Label";
                    case "TextBox": return "System.Windows.Forms.TextBox";
                    case "CheckBox": return "System.Windows.Forms.CheckBox";
                    case "RadioButton": return "System.Windows.Forms.RadioButton";
                    case "ListBox": return "System.Windows.Forms.ListBox";
                    case "ComboBox": return "System.Windows.Forms.ComboBox";
                    case "PictureBox": return "System.Windows.Forms.PictureBox";
                    case "Panel": return "System.Windows.Forms.Panel";
                    case "GroupBox": return "System.Windows.Forms.GroupBox";
                    case "TreeView": return "System.Windows.Forms.TreeView";
                    case "ListView": return "System.Windows.Forms.ListView";
                    case "ProgressBar": return "System.Windows.Forms.ProgressBar";
                    case "DateTimePicker": return "System.Windows.Forms.DateTimePicker";
                    case "RichTextBox": return "System.Windows.Forms.RichTextBox";
                    case "NumericUpDown": return "System.Windows.Forms.NumericUpDown";
                    case "LinkLabel": return "System.Windows.Forms.LinkLabel";
                    case "MenuStrip": return "System.Windows.Forms.MenuStrip";
                    case "TabControl": return "System.Windows.Forms.TabControl";
                    case "UserControl": return "System.Windows.Forms.UserControl";
                }
                return "System.Windows.Forms." + typeName;
            }
            return typeName;
        }
    }
}
