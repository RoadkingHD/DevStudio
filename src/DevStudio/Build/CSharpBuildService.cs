using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevStudio.Projects;
using DevStudio.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace DevStudio.Build
{
    /// <summary>Compiles C# sources in-process with Roslyn (AnyCPU / x86 / x64).</summary>
    public sealed class CSharpBuildService
    {
        private readonly Action<string> _log;

        public CSharpBuildService(Action<string> log)
        {
            _log = log ?? (s => { });
        }

        public Task<BuildResult> BuildAsync(ProjectBase project, string configuration)
        {
            return Task.Run(() => Build(project, configuration));
        }

        public BuildResult Build(ProjectBase project, string configuration)
        {
            var started = DateTime.UtcNow;
            var result = new BuildResult();
            _log("Build started: " + project.Name + " [" + configuration + "|" + project.Platform + "]");

            var syntaxTrees = new List<SyntaxTree>();
            foreach (var item in project.SourceFiles)
            {
                if (!File.Exists(item.FullPath))
                {
                    continue;
                }
                var text = File.ReadAllText(item.FullPath);
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(
                    text,
                    new CSharpParseOptions(LanguageVersion.CSharp7_3, DocumentationMode.Parse, SourceCodeKind.Regular),
                    path: item.FullPath,
                    encoding: Encoding.UTF8));
            }

            if (syntaxTrees.Count == 0)
            {
                result.Diagnostics.Add(new DiagnosticItem
                {
                    Severity = DiagnosticSeverityEx.Error,
                    Code = "DS0001",
                    Message = "The project contains no compilable source files.",
                    Project = project.Name
                });
                return result;
            }

            var references = WorkspaceManager.GetFrameworkReferences();
            foreach (var reference in project.References)
            {
                try
                {
                    var path = reference.Resolve(project.ProjectDirectory);
                    if (File.Exists(path))
                    {
                        references.Add(MetadataReference.CreateFromFile(path));
                        _log("  Reference: " + path);
                    }
                    else
                    {
                        result.Diagnostics.Add(new DiagnosticItem
                        {
                            Severity = DiagnosticSeverityEx.Warning,
                            Code = "DS0002",
                            Message = "Reference not found: " + path,
                            Project = project.Name
                        });
                    }
                }
                catch (Exception ex)
                {
                    result.Diagnostics.Add(new DiagnosticItem
                    {
                        Severity = DiagnosticSeverityEx.Warning,
                        Code = "DS0003",
                        Message = "Reference could not be loaded: " + reference + " (" + ex.Message + ")",
                        Project = project.Name
                    });
                }
            }

            var outputKind = project.OutputKind == OutputKindEx.Library ? OutputKind.DynamicallyLinkedLibrary :
                project.OutputKind == OutputKindEx.WindowsApplication ? OutputKind.WindowsApplication :
                OutputKind.ConsoleApplication;

            var platform = ParsePlatform(project.Platform);
            var compilation = CSharpCompilation.Create(
                project.AssemblyName,
                syntaxTrees,
                references,
                new CSharpCompilationOptions(outputKind)
                .WithPlatform(platform).WithAllowUnsafe(true)
                .WithOptimizationLevel(string.Equals(configuration, "Release", StringComparison.OrdinalIgnoreCase)
                    ? OptimizationLevel.Release
                    : OptimizationLevel.Debug));

            var outputDir = Path.IsPathRooted(project.OutputPath)
                ? project.OutputPath
                : Path.Combine(project.ProjectDirectory, project.OutputPath);
            Directory.CreateDirectory(outputDir);

            var exePath = Path.Combine(outputDir, project.OutputFileName);
            var pdbPath = Path.ChangeExtension(exePath, ".pdb");

            using (var exeStream = File.Create(exePath))
            using (var pdbStream = File.Create(pdbPath))
            {
                var emitOptions = new EmitOptions(
                    debugInformationFormat: DebugInformationFormat.PortablePdb,
                    pdbFilePath: pdbPath);

                var emitResult = compilation.Emit(exeStream, pdbStream, options: emitOptions);

                foreach (var diagnostic in emitResult.Diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Hidden)
                    {
                        continue;
                    }
                    var location = diagnostic.Location;
                    string file = null;
                    int line = 0, column = 0;
                    if (location != null && location.IsInSource)
                    {
                        var span = location.GetLineSpan();
                        file = span.Path;
                        line = span.StartLinePosition.Line + 1;
                        column = span.StartLinePosition.Character + 1;
                    }
                    result.Diagnostics.Add(new DiagnosticItem
                    {
                        Severity = diagnostic.Severity == DiagnosticSeverity.Error ? DiagnosticSeverityEx.Error
                            : diagnostic.Severity == DiagnosticSeverity.Warning ? DiagnosticSeverityEx.Warning
                            : DiagnosticSeverityEx.Message,
                        Code = diagnostic.Id,
                        Message = diagnostic.GetMessage(),
                        File = file,
                        Line = line,
                        Column = column,
                        Project = project.Name
                    });
                }

                result.Success = emitResult.Success;
                result.OutputPath = exePath;
            }

            result.Duration = DateTime.UtcNow - started;
            _log((result.Success ? "Build succeeded. -> " : "Build FAILED. ") + result.OutputPath);
            _log("  " + result.Diagnostics.Count + " diagnostic(s), " + (int)result.Duration.TotalMilliseconds + " ms");
            return result;
        }

        private static Microsoft.CodeAnalysis.Platform ParsePlatform(string platform)
        {
            switch ((platform ?? "AnyCPU").Trim().ToUpperInvariant())
            {
                case "X86":
                    return Microsoft.CodeAnalysis.Platform.X86;
                case "X64":
                    return Microsoft.CodeAnalysis.Platform.X64;
                default:
                    return Microsoft.CodeAnalysis.Platform.AnyCpu;
            }
        }
    }
}
