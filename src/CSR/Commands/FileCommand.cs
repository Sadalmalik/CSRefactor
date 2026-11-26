namespace CSR.Commands
{
    // Файловая команда анализа: имя, свои опции, форматы и анализ
    public class FileCommand
    {
        public required string Name { get; init; }

        // Свои опции и флаги сверх общих (--config, --project, --format, --out; с HasScope - --diff, --scope)
        public IReadOnlyCollection<string> Options { get; init; } = [];

        public IReadOnlyCollection<string> Flags { get; init; } = [];

        // Первый - по умолчанию
        public IReadOnlyList<string> Formats { get; init; } = ["json", "md"];

        // --diff и --scope: файлы диффа, находки - на измененных строках (changed-lines)
        public bool HasScope { get; init; }

        public string MissingFiles { get; init; } = "не заданы файлы или папки";

        public required Func<FileRun, FileResult> Analyze { get; init; }
    }
}
