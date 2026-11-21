using CSR.Workspace.Loading;
using CSR.Workspace.References;
using CSR.Workspace.Symbols;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Rename
{
    public class RenameSettings
    {
        // Сохранить старое имя для сериализованных данных вместо отказа
        public bool Migrate { get; init; }

        // Компиляция затронутых проектов до и после
        public bool Verify { get; init; } = true;

        // Строковые совпадения старого имени
        public bool ScanStrings { get; init; } = true;
    }

    // Переименование в памяти (Renamer): дифф, защита сериализуемых имен, строки, проверка компиляцией.
    // На диск пишет только Apply
    public static class RenameEngine
    {
        private const string UnityObject = "UnityEngine.Object";
        private const string UnityFileNote =
            "; Unity: имя файла MonoBehaviour и ScriptableObject - имя класса, .meta переносится вместе с файлом";
        private const string SerializableTypeNote =
            "тип [Serializable]: если хранится через [SerializeReference], имя типа записано в ассетах - нужен [MovedFrom]";

        public static async Task<RenamePlan> PlanAsync(LoadedSolution loaded, ISymbol requested, string newName,
            RenameSettings settings, RefactorOptions options, CancellationToken cancellation = default)
        {
            var symbol = GetRenameTarget(requested);
            ValidateName(symbol, newName);
            var serialized = SerializationGuard.Check(symbol, options.SerializationAttributes);
            var plan = new RenamePlan
            {
                Symbol = SymbolText.Signature(symbol),
                Kind = SymbolText.Kind(symbol),
                OldName = symbol.Name,
                NewName = newName,
                Serialized = serialized,
                Migrated = settings.Migrate,
            };
            if (plan.SerializationRefused)
                return plan;

            var solution = loaded.Current;
            var working = solution;
            var target = symbol;
            if (serialized.Count > 0)
            {
                var migration = await SerializationMigrator.ApplyAsync(solution, symbol, serialized, cancellation);
                working = migration.Solution;
                target = await RefindAsync(solution, migration, symbol, cancellation);
            }

            var renamed = await Renamer.RenameSymbolAsync(working, target, new SymbolRenameOptions(), newName,
                cancellation);
            var changes = await CollectChangesAsync(solution, renamed, loaded.Root, cancellation);
            var strings = settings.ScanStrings
                ? await StringMatchScanner.ScanAsync(solution, loaded.Root, symbol.Name, options, cancellation)
                : null;
            var compile = settings.Verify
                ? await CompileCheck.RunAsync(solution, renamed, loaded.Root, cancellation)
                : null;
            return new RenamePlan
            {
                Symbol = plan.Symbol,
                Kind = plan.Kind,
                OldName = plan.OldName,
                NewName = newName,
                Serialized = serialized,
                Migrated = settings.Migrate,
                Changes = changes,
                Strings = strings,
                Compile = compile,
                Notes = GetNotes(symbol, loaded.Root),
                Solution = renamed,
            };
        }

        // Запись измененных файлов с прежней кодировкой (BOM сохраняется); снимок решения - после правки
        public static void Apply(LoadedSolution loaded, RenamePlan plan)
        {
            if (plan.Solution == null)
                throw new InvalidOperationException("в плане нет правок");

            foreach (var change in plan.Changes)
                File.WriteAllText(change.Path, change.After.ToString(), DetectEncoding(change.Path));

            loaded.Update(plan.Solution);
        }

        private static ISymbol GetRenameTarget(ISymbol symbol)
        {
            if (!symbol.Locations.Any(location => location.IsInSource))
            {
                var name = SymbolText.Signature(symbol);
                throw new SymbolException($"{name}: символ из метаданных, переименовать нельзя");
            }

            return symbol switch
            {
                IMethodSymbol { AssociatedSymbol: { } associated } => associated,
                IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } method
                    => method.ContainingType,
                IMethodSymbol { MethodKind: MethodKind.Destructor } method => method.ContainingType,
                _ => symbol,
            };
        }

        private static void ValidateName(ISymbol symbol, string newName)
        {
            if (!SyntaxFacts.IsValidIdentifier(newName) || SyntaxFacts.GetKeywordKind(newName) != SyntaxKind.None)
                throw new SymbolException($"'{newName}' - не идентификатор C#");

            if (newName == symbol.Name)
                throw new SymbolException($"у символа уже имя '{newName}'");
        }

        // После миграции текст сдвинут: символ - в сдвинутой позиции того же объявления
        private static async Task<ISymbol> RefindAsync(Solution original, SerializationMigration migration,
            ISymbol symbol, CancellationToken cancellation)
        {
            var location = symbol.Locations.First(item => item.IsInSource);
            var documentId = original.GetDocumentId(location.SourceTree)!;
            var position = migration.MapPosition(documentId, location.SourceSpan.Start);
            var document = migration.Solution.GetDocument(documentId)!;
            var found = await SymbolFinder.FindSymbolAtPositionAsync(document, position, cancellation);
            if (found == null || found.Name != symbol.Name)
                throw new InvalidOperationException($"после миграции не найден {SymbolText.Signature(symbol)}");

            return found;
        }

        private static async Task<List<FileChange>> CollectChangesAsync(Solution before, Solution after, string root,
            CancellationToken cancellation)
        {
            var changes = new List<FileChange>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var documents = after.GetChanges(before)
                .GetProjectChanges()
                .SelectMany(project => project.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true));
            foreach (var documentId in documents)
            {
                var original = before.GetDocument(documentId)!;
                if (original.FilePath == null || !seen.Add(original.FilePath))
                    continue;

                var changed = after.GetDocument(documentId)!;
                var beforeText = await original.GetTextAsync(cancellation);
                var afterText = await changed.GetTextAsync(cancellation);
                var textChanges = (await changed.GetTextChangesAsync(original, cancellation))
                    .OrderBy(change => change.Span.Start)
                    .ToList();
                var file = SourceLocations.GetRelativePath(root, original.FilePath);
                changes.Add(new FileChange
                {
                    Path = original.FilePath,
                    File = file,
                    Before = beforeText,
                    After = afterText,
                    Diff = TextDiff.Build(file, beforeText, afterText, textChanges),
                });
            }

            return changes.OrderBy(change => change.File, StringComparer.Ordinal).ToList();
        }

        private static List<string> GetNotes(ISymbol symbol, string root)
        {
            var notes = new List<string>();
            if (symbol is not INamedTypeSymbol type)
                return notes;

            var unity = SerializationGuard.DerivesFrom(type, UnityObject);
            foreach (var location in type.Locations.Where(item => item.IsInSource))
            {
                var path = location.SourceTree!.FilePath;
                if (Path.GetFileNameWithoutExtension(path) != type.Name)
                    continue;

                var file = SourceLocations.GetRelativePath(root, path);
                notes.Add($"файл {file} назван по типу - переименовать вручную{(unity ? UnityFileNote : "")}");
            }

            if (type.IsSerializable)
                notes.Add(SerializableTypeNote);

            return notes;
        }

        private static System.Text.Encoding DetectEncoding(string path)
        {
            var preamble = new byte[3];
            using (var stream = File.OpenRead(path))
            {
                var read = stream.Read(preamble, 0, 3);
                if (read >= 2 && preamble[0] == 0xFF && preamble[1] == 0xFE)
                    return System.Text.Encoding.Unicode;

                if (read == 3 && preamble[0] == 0xEF && preamble[1] == 0xBB && preamble[2] == 0xBF)
                    return new System.Text.UTF8Encoding(true);
            }

            return new System.Text.UTF8Encoding(false);
        }
    }
}
