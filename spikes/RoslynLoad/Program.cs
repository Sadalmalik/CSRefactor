using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Rename;

// Замер: загрузка решения Unity через MSBuildWorkspace, семантика, поиск ссылок, переименование в памяти.
// Аргументы: <sln> <obj-dir> [проект] [метаданное имя типа]
// Ничего не пишет на диск, кроме промежуточных файлов design-time сборки в <obj-dir>.

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args[0] == "cc")
{
    ComplexityProbe.Run(args[1]);
    return;
}

var solutionPath = args[0];
var objDir = args[1];
var projectName = args.Length > 2 ? args[2] : null;
var typeName = args.Length > 3 ? args[3] : null;

var properties = new Dictionary<string, string>
{
    ["BaseIntermediateOutputPath"] = objDir + "\\",
    ["IntermediateOutputPath"] = objDir + "\\",
};

using var workspace = MSBuildWorkspace.Create(properties);
var failures = new List<string>();
workspace.RegisterWorkspaceFailedHandler(e => failures.Add($"{e.Diagnostic.Kind}: {e.Diagnostic.Message}"));

var total = Stopwatch.StartNew();
var solution = await workspace.OpenSolutionAsync(solutionPath);
Console.WriteLine($"open: {total.Elapsed.TotalSeconds:F1}s, projects {solution.Projects.Count()}, documents {solution.Projects.Sum(p => p.DocumentIds.Count)}");
Console.WriteLine($"workspace failures: {failures.Count}");
foreach (var group in failures.GroupBy(f => f.Length > 90 ? f[..90] : f).OrderByDescending(g => g.Count()).Take(8))
    Console.WriteLine($"  {group.Count()} x {group.Key}");

var meta = solution.Projects.Sum(p => p.MetadataReferences.Count);
Console.WriteLine($"metadata references total: {meta}, projects without references: {solution.Projects.Count(p => p.MetadataReferences.Count == 0)}");

if (projectName == null)
    return;

var project = solution.Projects.First(p => p.Name == projectName);
var sw = Stopwatch.StartNew();
var compilation = await project.GetCompilationAsync();
var errors = compilation!.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
Console.WriteLine($"compilation {project.Name}: {sw.Elapsed.TotalSeconds:F1}s, documents {project.DocumentIds.Count}, refs {project.MetadataReferences.Count}, project refs {project.ProjectReferences.Count()}, errors {errors.Count}");
foreach (var group in errors.GroupBy(d => d.Id).OrderByDescending(g => g.Count()).Take(5))
    Console.WriteLine($"  {group.Count()} x {group.Key}");

var monoBehaviour = compilation.GetTypeByMetadataName("UnityEngine.MonoBehaviour");
Console.WriteLine($"UnityEngine.MonoBehaviour resolved: {monoBehaviour != null}");

if (typeName == null)
    return;

foreach (var name in typeName.Split(','))
    await ProbeType(name);

Console.WriteLine($"total: {total.Elapsed.TotalSeconds:F1}s");

async Task ProbeType(string name)
{
    var type = compilation.GetTypeByMetadataName(name);
    if (type == null)
    {
        Console.WriteLine($"{name}: type not found");
        return;
    }

    var generatedParts = 0;
    foreach (var location in type.Locations.Where(l => l.IsInSource))
    {
        // документы генераторов кода GetDocument не возвращает
        if (solution.GetDocument(location.SourceTree) == null)
            generatedParts++;
    }

    Console.WriteLine($"type: declarations {type.Locations.Length}, in generated code {generatedParts}");

    var sw = Stopwatch.StartNew();
    var references = (await SymbolFinder.FindReferencesAsync(type, solution)).ToList();
    var locations = references.SelectMany(r => r.Locations).ToList();
    Console.WriteLine($"find references: {sw.Elapsed.TotalSeconds:F1}s, locations {locations.Count}, documents {locations.Select(l => l.Document.Id).Distinct().Count()}, projects {locations.Select(l => l.Document.Project.Id).Distinct().Count()}, in generated {locations.Count(l => l.Document is SourceGeneratedDocument)}");

    sw.Restart();
    var renamed = await Renamer.RenameSymbolAsync(solution, type, new SymbolRenameOptions(RenameFile: false), type.Name + "Renamed");
    var projectChanges = renamed.GetChanges(solution).GetProjectChanges().ToList();
    var changedDocuments = projectChanges.SelectMany(p => p.GetChangedDocuments()).ToList();
    Console.WriteLine($"rename in memory: {sw.Elapsed.TotalSeconds:F1}s, changed documents {changedDocuments.Count}, projects {projectChanges.Count}");

    sw.Restart();
    var errorsAfter = 0;
    var errorIds = new List<string>();
    foreach (var projectId in projectChanges.Select(p => p.ProjectId).Append(project.Id).Distinct())
    {
        var before = (await solution.GetProject(projectId)!.GetCompilationAsync())!.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error);
        var after = (await renamed.GetProject(projectId)!.GetCompilationAsync())!.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errorsAfter += after.Count - before;
        errorIds.AddRange(after.Select(d => $"{d.Id} {(d.Location.SourceTree?.FilePath.Contains("Generated") == true ? "gen" : "src")}"));
    }

    Console.WriteLine($"compile changed projects after rename: {sw.Elapsed.TotalSeconds:F1}s, new errors {errorsAfter}");
    foreach (var group in errorIds.GroupBy(e => e).OrderByDescending(g => g.Count()).Take(5))
        Console.WriteLine($"  {group.Count()} x {group.Key}");

    if (Environment.GetEnvironmentVariable("CSR_SPIKE_DETAILS") == null)
        return;

    // контроль: правка без смысла (комментарий в конце файла) — те же ошибки?
    var declarationDocument = solution.GetDocument(type.Locations[0].SourceTree)!;
    var text = await declarationDocument.GetTextAsync();
    var noop = solution.WithDocumentText(declarationDocument.Id, Microsoft.CodeAnalysis.Text.SourceText.From(text + "\n// noop\n"));
    var noopErrors = (await noop.GetProject(project.Id)!.GetCompilationAsync())!.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error);
    Console.WriteLine($"  control no-op edit: errors {noopErrors}");

    var renamedDiagnostics = (await renamed.GetProject(project.Id)!.GetCompilationAsync())!.GetDiagnostics();
    var originalDiagnostics = (await project.GetCompilationAsync())!.GetDiagnostics();
    // CS8784, CS8785 — генератор кода упал при инициализации или генерации
    Console.WriteLine($"  generator failures before {originalDiagnostics.Count(d => d.Id is "CS8784" or "CS8785")}, after {renamedDiagnostics.Count(d => d.Id is "CS8784" or "CS8785")}");
    foreach (var diagnostic in renamedDiagnostics.Where(d => d.Id is "CS8784" or "CS8785").Take(2))
        Console.WriteLine($"  {diagnostic.GetMessage()[..Math.Min(300, diagnostic.GetMessage().Length)]}");

    var detailed = renamedDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(1);
    foreach (var diagnostic in detailed)
        Console.WriteLine($"  {diagnostic.Location.GetLineSpan()} {diagnostic.GetMessage()}");
}
