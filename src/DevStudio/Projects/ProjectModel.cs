using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DevStudio.Projects
{
    public enum DevLanguage
    {
        CSharp,
        Cpp
    }

    public enum OutputKindEx
    {
        ConsoleApplication,
        WindowsApplication,
        Library
    }

    public sealed class ProjectItem
    {
        public string FullPath { get; }
        public string RelativePath { get; }
        public string DisplayName { get { return Path.GetFileName(FullPath); } }

        public ProjectItem(string basePath, string relativePath)
        {
            RelativePath = relativePath;
            FullPath = MakeAbsolute(basePath, relativePath);
        }

        public static string MakeAbsolute(string basePath, string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
            {
                return Path.GetFullPath(relativePath);
            }
            return Path.GetFullPath(Path.Combine(basePath, relativePath));
        }
    }

    public sealed class AssemblyReference
    {
        public string AssemblyName { get; set; }
        public string HintPath { get; set; }
        public bool IsFramework { get; set; }

        public string Resolve(string projectDir)
        {
            if (!string.IsNullOrEmpty(HintPath) && File.Exists(HintPath))
            {
                return Path.GetFullPath(HintPath);
            }
            if (!string.IsNullOrEmpty(HintPath) && !Path.IsPathRooted(HintPath))
            {
                var p = ProjectItem.MakeAbsolute(projectDir, HintPath);
                if (File.Exists(p))
                {
                    return p;
                }
            }
            if (!string.IsNullOrEmpty(HintPath) && File.Exists(HintPath))
            {
                return HintPath;
            }
            return AssemblyName + ".dll";
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(AssemblyName) ? HintPath : AssemblyName;
        }
    }

    /// <summary>Base class for every project handled by the studio.</summary>
    public abstract class ProjectBase
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public string ProjectDirectory
        {
            get { return string.IsNullOrEmpty(FilePath) ? Environment.CurrentDirectory : Path.GetDirectoryName(FilePath); }
        }
        public abstract DevLanguage Language { get; }
        public OutputKindEx OutputKind { get; set; }
        public string Platform { get; set; } = "AnyCPU";
        public string AssemblyName { get; set; }
        public List<ProjectItem> Items { get; } = new List<ProjectItem>();
        public List<AssemblyReference> References { get; } = new List<AssemblyReference>();

        public abstract string OutputPath { get; }
        public abstract string OutputFileName { get; }
        public abstract void Save();

        public string FullOutputPath
        {
            get
            {
                var dir = OutputPath;
                if (!Path.IsPathRooted(dir))
                {
                    dir = Path.Combine(ProjectDirectory, dir);
                }
                return Path.Combine(dir, OutputFileName);
            }
        }

        public IEnumerable<ProjectItem> SourceFiles
        {
            get
            {
                return Items.Where(i =>
                {
                    var ext = Path.GetExtension(i.FullPath).ToLowerInvariant();
                    return Language == DevLanguage.CSharp
                        ? ext == ".cs"
                        : ext == ".c" || ext == ".cpp" || ext == ".cc" || ext == ".cxx";
                });
            }
        }

        public static string DefaultOutputName(string assemblyName, OutputKindEx kind, DevLanguage lang)
        {
            if (kind == OutputKindEx.Library)
            {
                return assemblyName + (lang == DevLanguage.CSharp ? ".dll" : ".a");
            }
            return assemblyName + ".exe";
        }

        protected static string GetElementText(XElement root, string name)
        {
            var el = root.Descendants(name).FirstOrDefault();
            return el == null ? null : (el.Value ?? "").Trim();
        }
    }

    /// <summary>A "project" consisting of a single loose file.</summary>
    public sealed class SingleFileProject : ProjectBase
    {
        public string SourceFile { get; }

        public override DevLanguage Language
        {
            get
            {
                var ext = Path.GetExtension(SourceFile ?? "").ToLowerInvariant();
                return ext == ".c" || ext == ".cpp" || ext == ".cc" || ext == ".cxx" || ext == ".h" || ext == ".hpp"
                    ? DevLanguage.Cpp
                    : DevLanguage.CSharp;
            }
        }

        public SingleFileProject(string sourceFile)
        {
            SourceFile = Path.GetFullPath(sourceFile);
            Name = Path.GetFileNameWithoutExtension(SourceFile);
            AssemblyName = Name;
            OutputKind = OutputKindEx.ConsoleApplication;
            Platform = "AnyCPU";
            Items.Add(new ProjectItem(Path.GetDirectoryName(SourceFile), Path.GetFileName(SourceFile)));
        }

        public override string OutputPath
        {
            get { return Path.Combine(Path.GetDirectoryName(SourceFile), "bin"); }
        }

        public override string OutputFileName
        {
            get { return DefaultOutputName(AssemblyName, OutputKind, Language); }
        }

        public override void Save()
        {
        }
    }

    /// <summary>A C# project loaded from a .csproj file (legacy or SDK style).</summary>
    public sealed class CSharpProject : ProjectBase
    {
        public override DevLanguage Language { get { return DevLanguage.CSharp; } }

        public string OutputPathSetting { get; set; } = "bin\\Debug";
        public string RootNamespace { get; set; }
        public bool SdkStyle { get; private set; }

        public CSharpProject(string csprojPath)
        {
            FilePath = Path.GetFullPath(csprojPath);
            Load();
        }

        public static CSharpProject CreateNew(string csprojPath, string name, OutputKindEx kind)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Project ToolsVersion=\"15.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
            sb.AppendLine("  <PropertyGroup>");
            sb.AppendLine("    <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration>");
            sb.AppendLine("    <Platform Condition=\" '$(Platform)' == '' \">AnyCPU</Platform>");
            sb.AppendLine("    <OutputType>" + (kind == OutputKindEx.Library ? "Library" : kind == OutputKindEx.WindowsApplication ? "WinExe" : "Exe") + "</OutputType>");
            sb.AppendLine("    <RootNamespace>" + name + "</RootNamespace>");
            sb.AppendLine("    <AssemblyName>" + name + "</AssemblyName>");
            sb.AppendLine("    <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>");
            sb.AppendLine("    <OutputPath>bin\\Debug\\</OutputPath>");
            sb.AppendLine("  </PropertyGroup>");
            sb.AppendLine("  <ItemGroup>");
            sb.AppendLine("    <Reference Include=\"System\" />");
            sb.AppendLine("    <Reference Include=\"System.Core\" />");
            sb.AppendLine("    <Reference Include=\"System.Drawing\" />");
            sb.AppendLine("    <Reference Include=\"System.Windows.Forms\" />");
            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine("</Project>");
            Directory.CreateDirectory(Path.GetDirectoryName(csprojPath));
            File.WriteAllText(csprojPath, sb.ToString(), Encoding.UTF8);
            return new CSharpProject(csprojPath);
        }

        private void Load()
        {
            var doc = XDocument.Load(FilePath);
            var root = doc.Root;
            SdkStyle = root.Attribute("Sdk") != null;
            Name = Path.GetFileNameWithoutExtension(FilePath);
            AssemblyName = GetElementText(root, "AssemblyName") ?? Name;
            RootNamespace = GetElementText(root, "RootNamespace") ?? AssemblyName;

            var outputType = GetElementText(root, "OutputType");
            if (string.Equals(outputType, "WinExe", StringComparison.OrdinalIgnoreCase))
            {
                OutputKind = OutputKindEx.WindowsApplication;
            }
            else if (string.Equals(outputType, "Library", StringComparison.OrdinalIgnoreCase))
            {
                OutputKind = OutputKindEx.Library;
            }
            else
            {
                OutputKind = OutputKindEx.ConsoleApplication;
            }

            var platform = GetElementText(root, "PlatformTarget");
            Platform = string.IsNullOrEmpty(platform) ? "AnyCPU" : platform;

            var outPath = root.Descendants("OutputPath").FirstOrDefault();
            OutputPathSetting = outPath != null && !string.IsNullOrWhiteSpace(outPath.Value) ? outPath.Value.Trim() : "bin\\Debug";

            foreach (var compile in root.Descendants("Compile"))
            {
                var inc = (string)compile.Attribute("Include");
                if (!string.IsNullOrEmpty(inc))
                {
                    Items.Add(new ProjectItem(ProjectDirectory, inc));
                }
            }

            foreach (var none in root.Descendants("None"))
            {
                var inc = (string)none.Attribute("Include");
                if (!string.IsNullOrEmpty(inc) && inc.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    Items.Add(new ProjectItem(ProjectDirectory, inc));
                }
            }

            foreach (var content in root.Descendants("Content"))
            {
                var inc = (string)content.Attribute("Include");
                if (!string.IsNullOrEmpty(inc) && inc.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    Items.Add(new ProjectItem(ProjectDirectory, inc));
                }
            }

            foreach (var rf in root.Descendants("Reference"))
            {
                var name = (string)rf.Attribute("Include") ?? "";
                var shortName = name.Split(',')[0].Trim();
                if (shortName.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
                    shortName.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                    shortName == "mscorlib" || shortName == "netstandard")
                {
                    References.Add(new AssemblyReference { AssemblyName = shortName, IsFramework = true });
                    continue;
                }
                var hint = rf.Element("HintPath");
                References.Add(new AssemblyReference
                {
                    AssemblyName = shortName,
                    HintPath = hint != null ? ProjectItem.MakeAbsolute(ProjectDirectory, hint.Value.Trim()) : null
                });
            }

            if (Items.Count == 0)
            {
                foreach (var f in Directory.GetFiles(ProjectDirectory, "*.cs", SearchOption.AllDirectories))
                {
                    if (f.IndexOf(Path.Combine("obj"), StringComparison.OrdinalIgnoreCase) < 0 &&
                        f.IndexOf(Path.Combine("bin"), StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        Items.Add(new ProjectItem(ProjectDirectory, MakeRelative(ProjectDirectory, f)));
                    }
                }
            }
        }

        private static string MakeRelative(string basePath, string fullPath)
        {
            var baseUri = new Uri(basePath.EndsWith("\\") || basePath.EndsWith("/") ? basePath : basePath + Path.DirectorySeparatorChar);
            var rel = baseUri.MakeRelativeUri(new Uri(fullPath));
            return Uri.UnescapeDataString(rel.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        public void AddFile(string fullPath)
        {
            if (Items.Any(i => string.Equals(i.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            Items.Add(new ProjectItem(ProjectDirectory, MakeRelative(ProjectDirectory, fullPath)));
            Save();
        }

        public void AddReference(AssemblyReference reference)
        {
            References.Add(reference);
            Save();
        }

        public override string OutputPath
        {
            get { return OutputPathSetting; }
        }

        public override string OutputFileName
        {
            get { return AssemblyName + ".exe"; }
        }

        public override void Save()
        {
            var doc = XDocument.Load(FilePath);
            var root = doc.Root;
            if (root == null)
            {
                return;
            }

            var itemGroups = root.Elements("ItemGroup").ToList();
            XElement compileGroup = itemGroups.FirstOrDefault(g => g.Elements("Compile").Any());
            XElement refGroup = itemGroups.FirstOrDefault(g => g.Elements("Reference").Any());

            if (compileGroup == null)
            {
                compileGroup = new XElement("ItemGroup");
                root.Add(compileGroup);
            }

            foreach (var item in Items)
            {
                var rel = MakeRelative(ProjectDirectory, item.FullPath);
                if (!compileGroup.Elements("Compile").Any(e => string.Equals((string)e.Attribute("Include"), rel, StringComparison.OrdinalIgnoreCase)))
                {
                    compileGroup.Add(new XElement("Compile", new XAttribute("Include", rel)));
                }
            }

            var knownRefs = new HashSet<string>(References.Select(r => r.AssemblyName ?? r.HintPath), StringComparer.OrdinalIgnoreCase);
            if (refGroup == null)
            {
                refGroup = new XElement("ItemGroup");
                root.Add(refGroup);
            }
            foreach (var rf in refGroup.Elements("Reference").ToList())
            {
                var name = ((string)rf.Attribute("Include") ?? "").Split(',')[0].Trim();
                if (!knownRefs.Contains(name))
                {
                    rf.Remove();
                }
            }
            foreach (var r in References)
            {
                var existing = refGroup.Elements("Reference").FirstOrDefault(e =>
                    string.Equals(((string)e.Attribute("Include") ?? "").Split(',')[0].Trim(), r.AssemblyName ?? r.HintPath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    continue;
                }
                var el = new XElement("Reference", new XAttribute("Include", r.AssemblyName ?? Path.GetFileNameWithoutExtension(r.HintPath)));
                if (!string.IsNullOrEmpty(r.HintPath))
                {
                    el.Add(new XElement("HintPath", MakeRelative(ProjectDirectory, r.HintPath)));
                }
                refGroup.Add(el);
            }

            doc.Save(FilePath);
        }
    }

    /// <summary>A C/C++ project loaded from a .vcxproj file.</summary>
    public sealed class CppProject : ProjectBase
    {
        public override DevLanguage Language { get { return DevLanguage.Cpp; } }

        public List<string> IncludeDirectories { get; } = new List<string>();
        public List<string> Defines { get; } = new List<string>();
        public List<string> Libraries { get; } = new List<string>();

        public CppProject(string vcxprojPath)
        {
            FilePath = Path.GetFullPath(vcxprojPath);
            Load();
        }

        private void Load()
        {
            var doc = XDocument.Load(FilePath);
            var root = doc.Root;
            XNamespace ns = root.Name.Namespace;
            Name = Path.GetFileNameWithoutExtension(FilePath);
            AssemblyName = GetElementText(root, "TargetName") ?? Name;

            var configType = GetElementText(root, "ConfigurationType");
            if (string.Equals(configType, "DynamicLibrary", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(configType, "StaticLibrary", StringComparison.OrdinalIgnoreCase))
            {
                OutputKind = OutputKindEx.Library;
            }
            else if (string.Equals(configType, "Application", StringComparison.OrdinalIgnoreCase))
            {
                OutputKind = OutputKindEx.ConsoleApplication;
            }

            var platform = root.Descendants(ns + "Platform").FirstOrDefault();
            Platform = platform != null && platform.Value.Trim() == "x64" ? "x64" : "x86";

            foreach (var inc in root.Descendants(ns + "AdditionalIncludeDirectories"))
            {
                foreach (var part in SplitList(inc.Value))
                {
                    IncludeDirectories.Add(ProjectItem.MakeAbsolute(ProjectDirectory, part));
                }
            }

            foreach (var def in root.Descendants(ns + "PreprocessorDefinitions"))
            {
                foreach (var part in SplitList(def.Value))
                {
                    if (part != "$(DefineConstants)")
                    {
                        Defines.Add(part);
                    }
                }
            }

            foreach (var lib in root.Descendants(ns + "AdditionalDependencies"))
            {
                Libraries.AddRange(SplitList(lib.Value));
            }

            foreach (var compile in root.Descendants(ns + "ClCompile"))
            {
                var inc = (string)compile.Attribute("Include");
                if (!string.IsNullOrEmpty(inc))
                {
                    Items.Add(new ProjectItem(ProjectDirectory, inc));
                }
            }

            foreach (var include in root.Descendants(ns + "ClInclude"))
            {
                var inc = (string)include.Attribute("Include");
                if (!string.IsNullOrEmpty(inc))
                {
                    Items.Add(new ProjectItem(ProjectDirectory, inc));
                }
            }

            if (Items.Count == 0)
            {
                foreach (var f in Directory.GetFiles(ProjectDirectory, "*.cpp", SearchOption.AllDirectories))
                {
                    Items.Add(new ProjectItem(ProjectDirectory, MakeRelative(ProjectDirectory, f)));
                }
                foreach (var f in Directory.GetFiles(ProjectDirectory, "*.c", SearchOption.AllDirectories))
                {
                    Items.Add(new ProjectItem(ProjectDirectory, MakeRelative(ProjectDirectory, f)));
                }
            }
        }

        private static IEnumerable<string> SplitList(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                yield break;
            }
            foreach (var part in value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = part.Trim();
                if (p.Length > 0 && p[0] != '$')
                {
                    yield return p;
                }
            }
        }

        private static string MakeRelative(string basePath, string fullPath)
        {
            var baseUri = new Uri(basePath.EndsWith("\\") || basePath.EndsWith("/") ? basePath : basePath + Path.DirectorySeparatorChar);
            var rel = baseUri.MakeRelativeUri(new Uri(fullPath));
            return Uri.UnescapeDataString(rel.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        public void AddFile(string fullPath)
        {
            if (Items.Any(i => string.Equals(i.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            Items.Add(new ProjectItem(ProjectDirectory, MakeRelative(ProjectDirectory, fullPath)));
            Save();
        }

        public override string OutputPath
        {
            get { return Path.Combine("bin", Platform == "x64" ? "x64" : "x86"); }
        }

        public override string OutputFileName
        {
            get { return AssemblyName + ".exe"; }
        }

        public override void Save()
        {
            var doc = XDocument.Load(FilePath);
            var root = doc.Root;
            if (root == null)
            {
                return;
            }
            XNamespace ns = root.Name.Namespace;
            var compileGroup = root.Elements(ns + "ItemGroup").FirstOrDefault(g => g.Elements(ns + "ClCompile").Any());
            if (compileGroup == null)
            {
                compileGroup = new XElement(ns + "ItemGroup");
                root.Add(compileGroup);
            }
            foreach (var item in Items.Where(i =>
            {
                var ext = Path.GetExtension(i.FullPath).ToLowerInvariant();
                return ext == ".c" || ext == ".cpp" || ext == ".cc" || ext == ".cxx";
            }))
            {
                var rel = MakeRelative(ProjectDirectory, item.FullPath);
                if (!compileGroup.Elements(ns + "ClCompile").Any(e => string.Equals((string)e.Attribute("Include"), rel, StringComparison.OrdinalIgnoreCase)))
                {
                    compileGroup.Add(new XElement(ns + "ClCompile", new XAttribute("Include", rel)));
                }
            }
            doc.Save(FilePath);
        }
    }
}
