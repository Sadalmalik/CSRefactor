using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Refactor
{
    public enum FileChangeKind
    {
        Modified,
        Added,
        Removed,

        // Перенос файла без правок текста (файл .cs целиком, .meta Unity)
        Renamed,
    }

    // Правка одного файла в плане рефакторинга: текст до и после, дифф, вид правки
    public class FileChange
    {
        public FileChangeKind Kind { get; init; } = FileChangeKind.Modified;

        public required string Path { get; init; }

        // Путь относительно корня решения, разделитель '/'
        public required string File { get; init; }

        // Renamed: прежний полный путь
        public string? OldPath { get; init; }

        // Added: пустой текст; Renamed без текста (.meta) - пустые оба
        public required SourceText Before { get; init; }

        // Removed: пустой текст
        public required SourceText After { get; init; }

        // Added: кодировка нового файла
        public Encoding? Encoding { get; init; }

        public required string Diff { get; init; }
    }
}
