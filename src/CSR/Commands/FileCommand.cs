using System.Diagnostics;

namespace CSR.Commands
{
    // Файловая команда анализа: имя, свои опции, форматы и анализ
    public class FileCommand
    {
        private static readonly string[] CommonOptions = ["--config", "--project", "--format", "--out"];
        private static readonly string[] ScopeOptions = ["--diff", "--scope"];

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

        // Шаги: аргументы -> формат -> проект -> файлы -> анализ -> --out -> сводка со временем команды -> код возврата
        public int Run(IEnumerable<string> args)
        {
            var stopwatch = Stopwatch.StartNew();
            var optionNames = CommonOptions.Concat(HasScope ? ScopeOptions : []).Concat(Options);
            var commandLine = CommandLine.Parse(args, optionNames.ToHashSet(), Flags.ToHashSet());
            if (commandLine.Positional.Count == 0)
                throw new UsageException(MissingFiles);

            var format = commandLine.GetOption("--format") ?? Formats[0];
            if (!Formats.Contains(format))
                throw new UsageException($"неизвестный формат '{format}'");

            var run = FileRun.Create(commandLine, format, HasScope);
            var result = Analyze(run);
            Output.Write(result.Text, commandLine.GetOption("--out"));
            Console.Error.WriteLine($"csr {Name}: {result.Summary}, {stopwatch.Elapsed.TotalSeconds:F1} с");
            Console.Error.Write(result.Details);
            return result.ExitCode;
        }
    }
}
