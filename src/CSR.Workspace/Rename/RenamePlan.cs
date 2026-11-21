using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Rename
{
    public class FileChange
    {
        public required string Path { get; init; }

        // Путь относительно корня решения, разделитель '/'
        public required string File { get; init; }

        public required SourceText Before { get; init; }

        public required SourceText After { get; init; }

        public required string Diff { get; init; }
    }

    // Результат rename без записи: правки, причины отказа, места для ручной проверки
    public class RenamePlan
    {
        public required string Symbol { get; init; }

        public required string Kind { get; init; }

        public required string OldName { get; init; }

        public required string NewName { get; init; }

        public required IReadOnlyList<SerializedName> Serialized { get; init; }

        public bool Migrated { get; init; }

        public IReadOnlyList<FileChange> Changes { get; init; } = [];

        public StringScan? Strings { get; init; }

        public CompileResult? Compile { get; init; }

        // Замечания без отказа: файл назван по типу, тип [Serializable]
        public IReadOnlyList<string> Notes { get; init; } = [];

        // Решение после переименования (и миграции)
        public Solution? Solution { get; init; }

        // Отказ из-за сериализации: имя хранится в данных, а --migrate не задан или не поможет
        public bool SerializationRefused
            => Serialized.Count > 0 && (!Migrated || Serialized.Any(name => !name.CanMigrate));

        public bool HasNewErrors
            => Compile is { NewErrors.Count: > 0 };

        public bool HasStringMatches
            => Strings is { Total: > 0 };

        public string Diff
            => string.Concat(Changes.Select(change => change.Diff));
    }
}
