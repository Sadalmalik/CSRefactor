using System.Text;
using CSR.Workspace.References;
using CSR.Workspace.Rename;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Refactor
{
    // Правки решения в памяти как правки файлов: измененные, добавленные и удаленные документы с диффом;
    // запись на диск - только Apply
    public static class FileChanges
    {
        public static async Task<List<FileChange>> CollectAsync(
            Solution before,
            Solution after,
            string root,
            CancellationToken cancellation = default)
        {
            var changes = new List<FileChange>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var project in after.GetChanges(before).GetProjectChanges())
            {
                foreach (var documentId in project.GetChangedDocuments(onlyGetDocumentsWithTextChanges: true))
                {
                    var original = before.GetDocument(documentId)!;
                    if (original.FilePath != null && seen.Add(original.FilePath))
                        changes.Add(
                            await GetModifiedAsync(original, after.GetDocument(documentId)!, root, cancellation));
                }

                foreach (var documentId in project.GetAddedDocuments())
                {
                    var added = after.GetDocument(documentId)!;
                    if (added.FilePath != null && seen.Add(added.FilePath))
                        changes.Add(await GetAddedAsync(added, root, cancellation));
                }

                foreach (var documentId in project.GetRemovedDocuments())
                {
                    var removed = before.GetDocument(documentId)!;
                    if (removed.FilePath != null && seen.Add(removed.FilePath))
                        changes.Add(await GetRemovedAsync(removed, root, cancellation));
                }
            }

            return changes.OrderBy(change => change.File, StringComparer.Ordinal).ToList();
        }

        // Запись с прежней кодировкой файла (BOM сохраняется); новый файл - с кодировкой из плана
        public static void Apply(IReadOnlyList<FileChange> changes)
        {
            foreach (var change in changes)
            {
                switch (change.Kind)
                {
                    case FileChangeKind.Modified:
                        File.WriteAllText(change.Path, change.After.ToString(), DetectEncoding(change.Path));
                        break;
                    case FileChangeKind.Added:
                        Directory.CreateDirectory(Path.GetDirectoryName(change.Path)!);
                        File.WriteAllText(
                            change.Path,
                            change.After.ToString(),
                            change.Encoding ?? new UTF8Encoding(true));
                        break;
                    case FileChangeKind.Removed:
                        File.Delete(change.Path);
                        break;
                    case FileChangeKind.Renamed:
                        Directory.CreateDirectory(Path.GetDirectoryName(change.Path)!);
                        File.Move(change.OldPath!, change.Path);
                        break;
                }
            }
        }

        public static Encoding DetectEncoding(string path)
        {
            var preamble = new byte[3];
            using (var stream = File.OpenRead(path))
            {
                var read = stream.Read(preamble, 0, 3);
                if (read >= 2 && preamble[0] == 0xFF && preamble[1] == 0xFE)
                    return Encoding.Unicode;

                if (read == 3 && preamble[0] == 0xEF && preamble[1] == 0xBB && preamble[2] == 0xBF)
                    return new UTF8Encoding(true);
            }

            return new UTF8Encoding(false);
        }

        private static async Task<FileChange> GetModifiedAsync(
            Document original,
            Document changed,
            string root,
            CancellationToken cancellation)
        {
            var before = await original.GetTextAsync(cancellation);
            var after = await changed.GetTextAsync(cancellation);
            var textChanges = (await changed.GetTextChangesAsync(original, cancellation))
                .OrderBy(change => change.Span.Start)
                .ToList();
            var file = SourceLocations.GetRelativePath(root, original.FilePath!);
            return new FileChange
            {
                Path = original.FilePath!,
                File = file,
                Before = before,
                After = after,
                Diff = TextDiff.Build(file, before, after, textChanges),
            };
        }

        private static async Task<FileChange> GetAddedAsync(Document added, string root, CancellationToken cancellation)
        {
            var text = await added.GetTextAsync(cancellation);
            var file = SourceLocations.GetRelativePath(root, added.FilePath!);
            return new FileChange
            {
                Kind = FileChangeKind.Added,
                Path = added.FilePath!,
                File = file,
                Before = SourceText.From(""),
                After = text,
                Encoding = text.Encoding,
                Diff = TextDiff.BuildAdded(file, text),
            };
        }

        private static async Task<FileChange> GetRemovedAsync(
            Document removed,
            string root,
            CancellationToken cancellation)
        {
            var text = await removed.GetTextAsync(cancellation);
            var file = SourceLocations.GetRelativePath(root, removed.FilePath!);
            return new FileChange
            {
                Kind = FileChangeKind.Removed,
                Path = removed.FilePath!,
                File = file,
                Before = text,
                After = SourceText.From(""),
                Diff = TextDiff.BuildRemoved(file, text),
            };
        }
    }
}
