using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using DevStudio.Projects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace DevStudio.Services
{
    /// <summary>Manages the Roslyn workspace backing code completion, quick info and navigation.</summary>
    public sealed class WorkspaceManager
    {
        private static readonly WorkspaceManager _instance = new WorkspaceManager();
        public static WorkspaceManager Instance
        {
            get { return _instance; }
        }

        private readonly AdhocWorkspace _workspace = new AdhocWorkspace();
        private readonly Dictionary<string, DocumentId> _documentMap = new Dictionary<string, DocumentId>(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new object();
        private ProjectId _projectId;
        private CSharpProject _project;
        private CSharpCompilationOptions _compilationOptions;

        private WorkspaceManager()
        {
        }

        public AdhocWorkspace Workspace
        {
            get { return _workspace; }
        }

        public CSharpProject CurrentProject
        {
            get { return _project; }
        }

        public void LoadProject(CSharpProject project)
        {
            lock (_gate)
            {
                _workspace.ClearSolution();
                _documentMap.Clear();
                _project = project;

                var platform = ParsePlatform(project.Platform);
                _compilationOptions = new CSharpCompilationOptions(
                    project.OutputKind == OutputKindEx.Library ? OutputKind.DynamicallyLinkedLibrary :
                    project.OutputKind == OutputKindEx.WindowsApplication ? OutputKind.WindowsApplication :
                    OutputKind.ConsoleApplication)
                .WithPlatform(platform).WithAllowUnsafe(true)
                .WithOptimizationLevel(OptimizationLevel.Debug);

                _projectId = ProjectId.CreateNewId();
                var versionStamp = VersionStamp.Create();
                var projectInfo = ProjectInfo.Create(
                    _projectId,
                    versionStamp,
                    project.Name,
                    project.AssemblyName,
                    LanguageNames.CSharp,
                    compilationOptions: _compilationOptions,
                    parseOptions: new CSharpParseOptions(LanguageVersion.CSharp7_3, DocumentationMode.Parse, SourceCodeKind.Regular),
                    metadataReferences: BuildReferences(project));

                _workspace.AddProject(projectInfo);

                foreach (var item in project.SourceFiles)
                {
                    if (File.Exists(item.FullPath))
                    {
                        AddOrUpdateDocument(item.FullPath, File.ReadAllText(item.FullPath));
                    }
                }
            }
        }

        public void SetSingleFile(string filePath)
        {
            lock (_gate)
            {
                _workspace.ClearSolution();
                _documentMap.Clear();
                _project = null;

                _projectId = ProjectId.CreateNewId();
                var projectInfo = ProjectInfo.Create(
                    _projectId,
                    VersionStamp.Create(),
                    Path.GetFileNameWithoutExtension(filePath),
                    Path.GetFileNameWithoutExtension(filePath),
                    LanguageNames.CSharp,
                    compilationOptions: new CSharpCompilationOptions(OutputKind.ConsoleApplication).WithAllowUnsafe(true),
                    parseOptions: new CSharpParseOptions(LanguageVersion.CSharp7_3),
                    metadataReferences: GetFrameworkReferences());

                _workspace.AddProject(projectInfo);
                if (File.Exists(filePath))
                {
                    AddOrUpdateDocument(filePath, File.ReadAllText(filePath));
                }
            }
        }

        public DocumentId AddOrUpdateDocument(string filePath, string text)
        {
            lock (_gate)
            {
                DocumentId id;
                var source = SourceText.From(text ?? "");
                if (_documentMap.TryGetValue(filePath, out id))
                {
                    var doc = _workspace.CurrentSolution.GetDocument(id);
                    if (doc != null)
                    {
                        _workspace.TryApplyChanges(_workspace.CurrentSolution.WithDocumentText(id, source, PreservationMode.PreserveIdentity));
                    }
                    return id;
                }
                id = DocumentId.CreateNewId(_projectId);
                _documentMap[filePath] = id;
                _workspace.AddDocument(DocumentInfo.Create(
                    id,
                    Path.GetFileName(filePath),
                    folders: new[] { Path.GetDirectoryName(filePath) },
                    sourceCodeKind: SourceCodeKind.Regular,
                    loader: TextLoader.From(TextAndVersion.Create(source, VersionStamp.Create())),
                    filePath: filePath));
                return id;
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                _workspace.ClearSolution();
                _documentMap.Clear();
                _project = null;
                _projectId = null;
                _compilationOptions = null;
            }
        }

        public void RemoveDocument(string filePath)
        {
            lock (_gate)
            {
                DocumentId id;
                if (_documentMap.TryGetValue(filePath, out id))
                {
                    var doc = _workspace.CurrentSolution.GetDocument(id);
                    if (doc != null)
                    {
                        _workspace.TryApplyChanges(_workspace.CurrentSolution.RemoveDocument(id));
                    }
                    _documentMap.Remove(filePath);
                }
            }
        }

        public Document GetDocument(string filePath)
        {
            lock (_gate)
            {
                DocumentId id;
                return _documentMap.TryGetValue(filePath, out id) ? _workspace.CurrentSolution.GetDocument(id) : null;
            }
        }

        public string GetFilePath(DocumentId id)
        {
            lock (_gate)
            {
                return _documentMap.FirstOrDefault(kv => kv.Value == id).Key;
            }
        }

        public void UpdateCompilationOptions()
        {
            if (_project == null || _compilationOptions == null)
            {
                return;
            }
            lock (_gate)
            {
                _compilationOptions = _compilationOptions.WithPlatform(ParsePlatform(_project.Platform));
                var solution = _workspace.CurrentSolution.WithProjectCompilationOptions(_projectId, _compilationOptions);
                _workspace.TryApplyChanges(solution);
            }
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

        public static List<MetadataReference> GetFrameworkReferences()
        {
            var list = new List<MetadataReference>();
            var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
            if (!string.IsNullOrEmpty(runtimeDir) && Directory.Exists(runtimeDir))
            {
                foreach (var dll in Directory.GetFiles(runtimeDir, "*.dll"))
                {
                    var name = Path.GetFileNameWithoutExtension(dll);
                    if (name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                        name == "mscorlib" || name == "netstandard" || name == "WindowsBase")
                    {
                        if (!IsManagedAssembly(dll))
                        {
                            continue;
                        }
                        try
                        {
                            list.Add(MetadataReference.CreateFromFile(dll));
                        }
                        catch
                        {
                        }
                    }
                }
            }
            return list;
        }

        public List<MetadataReference> BuildReferences(CSharpProject project)
        {
            var list = GetFrameworkReferences();
            foreach (var reference in project.References)
            {
                if (reference.IsFramework)
                {
                    continue;
                }
                try
                {
                    var path = reference.Resolve(project.ProjectDirectory);
                    if (File.Exists(path) && IsManagedAssembly(path))
                    {
                        list.Add(MetadataReference.CreateFromFile(path));
                    }
                }
                catch
                {
                }
            }
            return list;
        }

        public static bool IsManagedAssembly(string path)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var pe = new PEReader(stream))
                {
                    if (!pe.HasMetadata)
                    {
                        return false;
                    }
                    return System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe).IsAssembly;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
