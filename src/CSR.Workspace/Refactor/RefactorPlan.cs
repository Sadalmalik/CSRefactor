using CSR.Workspace.Rename;
using Microsoft.CodeAnalysis;

namespace CSR.Workspace.Refactor
{
    // Итог move / extract: preview - дифф без записи; applied - записано; refused - отказ с причиной;
    // blocked - --apply не выполнен (новые ошибки компиляции без --force)
    public enum RefactorStatus
    {
        Preview,
        Applied,
        Refused,
        Blocked,
    }

    // Результат move / extract без записи: правки файлов, причина отказа, замечания, проверка компиляцией
    public class RefactorPlan
    {
        // move, extract
        public required string Command { get; init; }

        // Что переносится или извлекается: символ, файл:строки
        public required string Subject { get; init; }

        // Куда: файл, тип, имя нового метода
        public required string Target { get; init; }

        public string? Refusal { get; init; }

        public IReadOnlyList<FileChange> Changes { get; init; } = [];

        public CompileResult? Compile { get; init; }

        public IReadOnlyList<string> Notes { get; init; } = [];

        public Solution? Solution { get; init; }

        public bool HasNewErrors
            => Compile is { NewErrors.Count: > 0 };

        public string Diff
            => string.Concat(Changes.Select(change => change.Diff));

        public static RefactorPlan Refuse(string command, string subject, string target, string reason)
            => new() { Command = command, Subject = subject, Target = target, Refusal = reason };

        public RefactorStatus GetStatus(bool apply, bool force)
        {
            if (Refusal != null)
                return RefactorStatus.Refused;

            if (!apply)
                return RefactorStatus.Preview;

            return HasNewErrors && !force ? RefactorStatus.Blocked : RefactorStatus.Applied;
        }
    }
}
