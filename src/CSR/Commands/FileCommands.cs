using System.Diagnostics;
using CSR.Core.Checks;
using CSR.Core.Config;

namespace CSR.Commands
{
    public static class FileCommands
    {
        private static readonly string[] CommonOptions = ["--config", "--project", "--format", "--out"];
        private static readonly string[] ScopeOptions = ["--diff", "--scope"];

        // Шаги: аргументы -> формат -> проект -> файлы -> анализ -> --out -> сводка со временем команды -> код возврата
        public static int Run(FileCommand command, IEnumerable<string> args)
        {
            var stopwatch = Stopwatch.StartNew();
            var optionNames = CommonOptions.Concat(command.HasScope ? ScopeOptions : []).Concat(command.Options);
            var commandLine = CommandLine.Parse(args, optionNames.ToHashSet(), command.Flags.ToHashSet());
            if (commandLine.Positional.Count == 0)
                throw new UsageException(command.MissingFiles);

            var format = commandLine.GetOption("--format") ?? command.Formats[0];
            if (!command.Formats.Contains(format))
                throw new UsageException($"неизвестный формат '{format}'");

            var run = CreateRun(commandLine, format, command.HasScope);
            var result = command.Analyze(run);
            Output.Write(result.Text, commandLine.GetOption("--out"));
            Console.Error.WriteLine($"csr {command.Name}: {result.Summary}, {stopwatch.Elapsed.TotalSeconds:F1} с");
            Console.Error.Write(result.Details);
            return result.ExitCode;
        }

        // --scope: без --diff - file, с --diff - changed-lines (без --diff - от HEAD); при диффе файлы - только из него
        private static FileRun CreateRun(CommandLine commandLine, string format, bool hasScope)
        {
            var diff = hasScope ? commandLine.GetOption("--diff") : null;
            var defaultScope = diff == null ? "file" : "changed-lines";
            var scope = hasScope ? commandLine.GetOption("--scope") ?? defaultScope : "file";
            if (scope is not ("file" or "changed-lines"))
                throw new UsageException($"неизвестный режим '{scope}'");

            var directory = Environment.CurrentDirectory;
            var start = ConfigFiles.GetStart(commandLine.Positional[0], directory);
            var configs = new CommandConfigs(commandLine, directory, start, Console.Error);
            var project = configs.LoadProject();
            var files = SourceFiles.Collect(commandLine.Positional);
            Dictionary<string, ChangedLines>? changed = null;
            if (diff != null || scope == "changed-lines")
            {
                changed = GitDiff.Collect(diff ?? "HEAD", commandLine.Positional[0]);
                files = files.Where(changed.ContainsKey).ToList();
            }

            return new FileRun
            {
                CommandLine = commandLine,
                Format = format,
                Configs = configs,
                Project = project,
                Files = files,
                Scope = scope,
                Changed = scope == "changed-lines" ? changed : null,
            };
        }
    }
}
