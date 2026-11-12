using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DevStudio.Projects;

namespace DevStudio.Build
{
    /// <summary>Builds C/C++ projects by invoking an external MinGW (gcc/g++) toolchain.</summary>
    public sealed class CppBuildService
    {
        private static readonly Regex GccErrorRegex = new Regex(
            @"^(?<file>[^:]+):(?<line>\d+):(?<col>\d+):\s+(?<severity>error|warning|note):\s*(?<message>.*)$",
            RegexOptions.Compiled);

        private readonly Action<string> _log;
        public string CompilerPath { get; set; }

        public CppBuildService(Action<string> log)
        {
            _log = log ?? (s => { });
        }

        public static string FindGpp()
        {
            foreach (var name in new[] { "g++.exe", "g++", "gcc.exe", "gcc" })
            {
                var found = FindOnPath(name);
                if (found != null)
                {
                    return found;
                }
            }
            foreach (var dir in Directory.GetDirectories(@"C:\", "mingw*", SearchOption.TopDirectoryOnly))
            {
                foreach (var candidate in Directory.GetFiles(dir, "g++.exe", SearchOption.AllDirectories))
                {
                    return candidate;
                }
            }
            foreach (var dir in Directory.GetDirectories(@"C:\Program Files", "mingw*", SearchOption.TopDirectoryOnly))
            {
                foreach (var candidate in Directory.GetFiles(dir, "g++.exe", SearchOption.AllDirectories))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static string FindOnPath(string fileName)
        {
            var pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim(), fileName);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
                catch
                {
                }
            }
            return null;
        }

        public Task<BuildResult> BuildAsync(ProjectBase project, string configuration)
        {
            return Task.Run(() => Build(project, configuration));
        }

        public BuildResult Build(ProjectBase project, string configuration)
        {
            var started = DateTime.UtcNow;
            var result = new BuildResult();

            var compiler = CompilerPath ?? FindGpp();
            if (compiler == null)
            {
                result.Diagnostics.Add(new DiagnosticItem
                {
                    Severity = DiagnosticSeverityEx.Error,
                    Code = "DS0101",
                    Message = "MinGW compiler (g++) not found. Install MinGW-w64 or set the compiler path in Tools > Options.",
                    Project = project.Name
                });
                _log("Build FAILED: g++ not found.");
                return result;
            }

            var sources = project.SourceFiles.Select(i => i.FullPath).Where(File.Exists).ToList();
            if (sources.Count == 0)
            {
                result.Diagnostics.Add(new DiagnosticItem
                {
                    Severity = DiagnosticSeverityEx.Error,
                    Code = "DS0102",
                    Message = "The project contains no C/C++ source files.",
                    Project = project.Name
                });
                return result;
            }

            var outputDir = Path.IsPathRooted(project.OutputPath)
                ? project.OutputPath
                : Path.Combine(project.ProjectDirectory, project.OutputPath);
            Directory.CreateDirectory(outputDir);
            var exePath = Path.Combine(outputDir, project.OutputFileName);

            var args = new StringBuilder();
            args.Append(string.Equals(configuration, "Release", StringComparison.OrdinalIgnoreCase) ? "-O2 " : "-g -O0 ");
            args.Append("-Wall -std=c++17 ");
            if (project.Platform == "x64")
            {
                args.Append("-m64 ");
            }
            else
            {
                args.Append("-m32 ");
            }
            if (project.OutputKind == OutputKindEx.WindowsApplication)
            {
                args.Append("-mwindows ");
            }

            var cppProject = project as CppProject;
            if (cppProject != null)
            {
                foreach (var include in cppProject.IncludeDirectories)
                {
                    args.Append("-I\"" + include + "\" ");
                }
                foreach (var define in cppProject.Defines)
                {
                    args.Append("-D\"" + define + "\" ");
                }
                if (cppProject.OutputKind == OutputKindEx.Library)
                {
                    args.Append("-shared ");
                }
            }

            foreach (var source in sources)
            {
                args.Append("\"" + source + "\" ");
            }
            args.Append("-o \"" + exePath + "\"");

            _log("Build started: " + project.Name + " [" + configuration + "|" + project.Platform + "]");
            _log(compiler + " " + args);

            var startInfo = new ProcessStartInfo
            {
                FileName = compiler,
                Arguments = args.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = project.ProjectDirectory,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    var stdout = process.StandardOutput.ReadToEnd();
                    var stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    foreach (var line in SplitLines(stdout))
                    {
                        _log("  " + line);
                    }
                    foreach (var line in SplitLines(stderr))
                    {
                        _log("  " + line);
                        var match = GccErrorRegex.Match(line);
                        if (match.Success)
                        {
                            result.Diagnostics.Add(new DiagnosticItem
                            {
                                Severity = match.Groups["severity"].Value == "error" ? DiagnosticSeverityEx.Error
                                    : match.Groups["severity"].Value == "warning" ? DiagnosticSeverityEx.Warning
                                    : DiagnosticSeverityEx.Message,
                                Code = "gcc",
                                Message = match.Groups["message"].Value,
                                File = match.Groups["file"].Value,
                                Line = int.Parse(match.Groups["line"].Value),
                                Column = int.Parse(match.Groups["col"].Value),
                                Project = project.Name
                            });
                        }
                    }

                    result.Success = process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                result.Diagnostics.Add(new DiagnosticItem
                {
                    Severity = DiagnosticSeverityEx.Error,
                    Code = "DS0103",
                    Message = "Compiler invocation failed: " + ex.Message,
                    Project = project.Name
                });
                result.Success = false;
            }

            result.OutputPath = exePath;
            result.Duration = DateTime.UtcNow - started;
            _log(result.Success
                ? "Build succeeded. -> " + exePath
                : "Build FAILED. " + result.Diagnostics.Count(d => d.Severity == DiagnosticSeverityEx.Error) + " error(s)");
            return result;
        }

        public void Run(string exePath, Action<string> outputLine)
        {
            if (!File.Exists(exePath))
            {
                outputLine("Output file not found: " + exePath);
                return;
            }
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exePath)
            };
            var process = Process.Start(startInfo);
            process.OutputDataReceived += (s, e) => { if (e.Data != null) outputLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) outputLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Task.Run(() =>
            {
                process.WaitForExit();
                outputLine("Program exited with code " + process.ExitCode + ".");
            });
        }

        private static IEnumerable<string> SplitLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }
            foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Length > 0)
                {
                    yield return line;
                }
            }
        }
    }
}
