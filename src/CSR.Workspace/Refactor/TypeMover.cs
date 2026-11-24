using System.Text;
using CSR.Core.Text;
using CSR.Workspace.Loading;
using CSR.Workspace.References;
using CSR.Workspace.Symbols;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Refactor
{
    // Перенос типа верхнего уровня в свой файл (имя файла - имя типа). Тип не один в файле - новый файл
    // с теми же пространствами имен и нужными директивами using, в исходном убираются строки типа и ставшие
    // лишними using. Тип один в файле - перенос файла целиком, .meta Unity переносится с ним (GUID сохраняется).
    // Текст типа не переформатируется: строки берутся как есть
    public static class TypeMover
    {
        private const string Command = "move";
        private const string UnityObject = "UnityEngine.Object";

        public static async Task<RefactorPlan> PlanAsync(
            LoadedSolution loaded,
            INamedTypeSymbol type,
            string? target,
            string workingDirectory,
            bool verify,
            CancellationToken cancellation = default)
        {
            var subject = SymbolText.Qualified(type);
            var refusal = GetTypeRefusal(type);
            if (refusal != null)
                return RefactorPlan.Refuse(Command, subject, target ?? type.Name + ".cs", refusal);

            var reference = type.DeclaringSyntaxReferences[0];
            var declaration = (MemberDeclarationSyntax)await reference.GetSyntaxAsync(cancellation);
            var solution = loaded.Current;
            var document = solution.GetDocument(declaration.SyntaxTree);
            if (document is null or SourceGeneratedDocument || document.FilePath == null)
                return RefactorPlan.Refuse(Command, subject, target ?? type.Name + ".cs", "тип в сгенерированном коде");

            var source = document.FilePath;
            var targetPath = target == null
                ? Path.Combine(Path.GetDirectoryName(source)!, type.Name + ".cs")
                : Path.GetFullPath(Path.Combine(workingDirectory, target));
            var targetFile = SourceLocations.GetRelativePath(loaded.Root, targetPath);
            refusal = GetTargetRefusal(loaded, document, source, targetPath);
            if (refusal != null)
                return RefactorPlan.Refuse(Command, subject, targetFile, refusal);

            var root = (CompilationUnitSyntax)await declaration.SyntaxTree.GetRootAsync(cancellation);
            var others = GetTopLevelTypes(root).Where(other => other != declaration).ToList();
            var text = await document.GetTextAsync(cancellation);
            var notes = new List<string>();
            Solution after;
            List<FileChange> changes;
            if (others.Count == 0 && !root.Members.OfType<GlobalStatementSyntax>().Any())
            {
                after = MoveDocument(solution, document, targetPath, text);
                changes = GetFileMoves(loaded.Root, source, targetPath, text, notes);
            }
            else
            {
                var model = (await document.GetSemanticModelAsync(cancellation))!;
                var newText = BuildTypeFile(text, root, declaration, model);
                var sourceText = RemoveType(text, root, declaration, others, model);
                after = solution.WithDocumentText(document.Id, sourceText);
                after = AddDocument(after, document.Project, targetPath, newText, text.Encoding);
                changes = await FileChanges.CollectAsync(solution, after, loaded.Root, cancellation);
                if (SerializationGuard.DerivesFrom(type, UnityObject))
                    notes.Add("Unity: новый файл получит свой .meta при импорте - ссылки ассетов на скрипт проверить");
            }

            AddProjectNote(document.Project, notes);
            var compile = verify ? await CompileCheck.RunAsync(solution, after, loaded.Root, cancellation) : null;
            return new RefactorPlan
            {
                Command = Command,
                Subject = subject,
                Target = targetFile,
                Changes = changes,
                Compile = compile,
                Notes = notes,
                Solution = after,
            };
        }

        private static string? GetTypeRefusal(INamedTypeSymbol type)
        {
            if (!type.Locations.Any(location => location.IsInSource))
                return "тип из метаданных";

            if (type.ContainingType != null)
                return "вложенный тип: перенос меняет его имя и доступ - не переносится";

            var declarations = type.DeclaringSyntaxReferences.Length;
            return declarations > 1 ? $"partial-тип: объявлений {declarations} - перенос по частям не поддержан" : null;
        }

        private static string? GetTargetRefusal(LoadedSolution loaded, Document document, string source, string target)
        {
            if (!target.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return "файл назначения - не .cs";

            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                return "тип уже в файле со своим именем";

            if (File.Exists(target) || loaded.Contains(target))
                return $"файл {Path.GetFileName(target)} уже есть";

            var projectFolder = Path.GetDirectoryName(document.Project.FilePath);
            if (projectFolder == null || !IsInside(target, projectFolder))
                return "файл назначения вне папки проекта: проект его не увидит";

            return null;
        }

        private static List<MemberDeclarationSyntax> GetTopLevelTypes(CompilationUnitSyntax root)
        {
            return root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
                .OfType<MemberDeclarationSyntax>()
                .Where(member => member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
                .ToList();
        }

#region Тип один в файле

        private static Solution MoveDocument(Solution solution, Document document, string target, SourceText text)
        {
            var removed = solution.RemoveDocument(document.Id);
            return AddDocument(removed, document.Project, target, text, text.Encoding);
        }

        // Перенос файла и его .meta без правок текста
        private static List<FileChange> GetFileMoves(
            string root,
            string source,
            string target,
            SourceText text,
            List<string> notes)
        {
            var empty = SourceText.From("");
            var changes = new List<FileChange> { CreateMove(root, source, target, text) };
            var meta = source + ".meta";
            if (File.Exists(meta))
            {
                changes.Add(CreateMove(root, meta, target + ".meta", empty));
                notes.Add("Unity: .meta переносится вместе с файлом, GUID сохраняется");
            }

            notes.Add("в файле нет других типов: файл переносится целиком");
            return changes;
        }

        private static FileChange CreateMove(string root, string from, string to, SourceText text)
        {
            var file = SourceLocations.GetRelativePath(root, to);
            var oldFile = SourceLocations.GetRelativePath(root, from);
            return new FileChange
            {
                Kind = FileChangeKind.Renamed,
                Path = to,
                OldPath = from,
                File = file,
                Before = text,
                After = text,
                Diff = TextDiff.BuildRenamed(oldFile, file),
            };
        }

#endregion


#region Тип не один в файле

        // Новый файл: директивы using файла, нужные типу; те же объявления пространств имен; строки типа как есть
        private static SourceText BuildTypeFile(
            SourceText text,
            CompilationUnitSyntax root,
            MemberDeclarationSyntax declaration,
            SemanticModel model)
        {
            var lineBreak = TextLayout.GetLineBreak(text, text.Lines.GetLineFromPosition(declaration.SpanStart).LineNumber);
            var usage = UsingAnalysis.Collect(model, [declaration]);
            var lines = new List<string>();
            foreach (var alias in root.Externs)
                lines.Add(alias.ToString());

            AddUsings(lines, root.Usings, model, usage, text);
            var namespaces = declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().ToList();
            var closing = new List<string>();
            foreach (var space in namespaces)
            {
                AddBlank(lines);
                var indent = TextLayout.GetIndent(text, space.SpanStart);
                if (space is FileScopedNamespaceDeclarationSyntax)
                {
                    lines.Add($"{indent}namespace {space.Name};");
                }
                else
                {
                    var block = (NamespaceDeclarationSyntax)space;
                    lines.Add($"{indent}namespace {space.Name}");
                    lines.Add(TextLayout.GetIndent(text, block.OpenBraceToken.SpanStart) + "{");
                    closing.Insert(0, TextLayout.GetIndent(text, block.CloseBraceToken.SpanStart) + "}");
                }

                var before = lines.Count;
                AddUsings(lines, space.Usings, model, usage, text);
                if (lines.Count > before || space is FileScopedNamespaceDeclarationSyntax)
                    AddBlank(lines);
            }

            if (namespaces.Count == 0)
                AddBlank(lines);

            var typeLines = SourceLines.GetNodeLines(text, declaration);
            lines.Add(text.ToString(typeLines).TrimEnd('\r', '\n'));
            lines.AddRange(closing);
            var content = string.Join(lineBreak, lines.SkipWhile(line => line.Length == 0)) + lineBreak;
            return SourceText.From(content, text.Encoding ?? new UTF8Encoding(true));
        }

        // Исходный файл без строк типа и без директив using, нужных только ему
        private static SourceText RemoveType(
            SourceText text,
            CompilationUnitSyntax root,
            MemberDeclarationSyntax declaration,
            List<MemberDeclarationSyntax> others,
            SemanticModel model)
        {
            var moved = UsingAnalysis.Collect(model, [declaration]);
            var rest = UsingAnalysis.Collect(model, others.Concat<SyntaxNode>(root.AttributeLists));
            var spans = new List<TextSpan> { SourceLines.GetNodeLines(text, declaration) };
            foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                bool isLost = UsingAnalysis.IsNeeded(model, directive, moved)
                    && !UsingAnalysis.IsNeeded(model, directive, rest);
                if (isLost)
                    spans.Add(SourceLines.GetNodeLines(text, directive));
            }

            return SourceLines.RemoveLines(text, spans);
        }

        private static void AddUsings(
            List<string> lines,
            SyntaxList<UsingDirectiveSyntax> usings,
            SemanticModel model,
            UsingUsage usage,
            SourceText text)
        {
            foreach (var directive in usings)
            {
                if (directive.GlobalKeyword == default && UsingAnalysis.IsNeeded(model, directive, usage))
                    lines.Add(TextLayout.GetIndent(text, directive.SpanStart) + directive.ToString());
            }
        }

        private static void AddBlank(List<string> lines)
        {
            if (lines.Count > 0 && lines[^1].Length > 0)
                lines.Add("");
        }

#endregion

        private static Solution AddDocument(
            Solution solution,
            Project project,
            string path,
            SourceText text,
            Encoding? encoding)
        {
            var projectFolder = Path.GetDirectoryName(project.FilePath)!;
            var relative = Path.GetRelativePath(projectFolder, Path.GetDirectoryName(path)!);
            string[] folders = relative == "." ? [] : relative.Split(Path.DirectorySeparatorChar);
            var id = DocumentId.CreateNewId(project.Id);
            var content = text.Encoding != null || encoding == null ? text : SourceText.From(text.ToString(), encoding);
            return solution.AddDocument(id, Path.GetFileName(path), content, folders, path);
        }

        // Проект перечисляет файлы явно (Unity): новый файл в .csproj не попадет до пересоздания проекта
        private static void AddProjectNote(Project project, List<string> notes)
        {
            if (project.FilePath == null || !File.Exists(project.FilePath))
                return;

            if (File.ReadAllText(project.FilePath).Contains("<Compile Include=", StringComparison.Ordinal))
                notes.Add("проект перечисляет файлы явно: Unity пересоздаст .csproj, иначе добавить <Compile Include>");
        }

        private static bool IsInside(string path, string folder)
        {
            var relative = Path.GetRelativePath(folder, path);
            return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
        }
    }
}
