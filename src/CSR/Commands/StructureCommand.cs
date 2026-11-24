using System.Diagnostics;
using CSR.Core.Structure;
using CSR.Reports.Config;
using CSR.Reports.Structure;

namespace CSR.Commands
{
    // csr structure: структурные метрики на графе типов - глубина владения и наследования, объекты-боги, циклы и
    // свалки папок и пространств имен, концентраторы. Ничего не пишет рядом с кодом
    public static class StructureCommand
    {
        public const string Usage = """
            csr structure <файлы | папки>... [опции]

              --config <structure.jsonc>   пороги отметок и размер топов; без --config - .csr/structure.jsonc в папке
                                           цели или выше, иначе config/structure.jsonc рядом с csr.exe, иначе по умолчанию
              --project <project.jsonc>    символы препроцессора; поиск - как у style
              --top <N>                    размер топов (по умолчанию 10)
              --format json | md           формат отчета (по умолчанию json)
              --out <файл>                 записать отчет в файл вместо stdout

            Граф типов строится на компиляции файлов цели без загрузки решения: типы сторонних библиотек не разрешены.
            Коды возврата: 0 - успех (отметки ошибкой не считаются), 2 - ошибка запуска
            """;

        private static readonly HashSet<string> OptionNames =
            ["--config", "--project", "--format", "--out", .. StructureConfig.Table.OptionNames];

        public static int Run(IEnumerable<string> args)
        {
            var commandLine = CommandLine.Parse(args, OptionNames, new HashSet<string>());
            if (commandLine.Positional.Count == 0)
                throw new UsageException("не заданы файлы или папки");

            var format = commandLine.GetOption("--format") ?? "json";
            if (format is not ("json" or "md"))
                throw new UsageException($"неизвестный формат '{format}'");

            var options = LoadOptions(commandLine);
            var project = StyleCommand.LoadProject(commandLine);
            var stopwatch = Stopwatch.StartNew();
            var files = SourceFiles.Collect(commandLine.Positional);
            var source = SourceGraph.Build(SourceGraph.Parse(files, project));
            var result = StructureAnalysis.Run(
                source.Graph,
                [source.Compilation],
                source.Trees,
                options,
                CancellationToken.None);
            var report = new StructureReport { FileCount = files.Count, Options = options, Result = result };
            var text = format == "md" ? StructureMarkdownWriter.Write(report) : StructureJsonWriter.Write(report);
            Output.Write(text, commandLine.GetOption("--out"));

            var seconds = stopwatch.Elapsed.TotalSeconds;
            Console.Error.WriteLine($"csr structure: файлов {files.Count}, типов {result.Graph.Nodes.Count}, "
                + $"ребер {result.Graph.EdgeCount}, {seconds:F1} с");
            return 0;
        }

        private static StructureOptions LoadOptions(CommandLine commandLine)
        {
            var start = ConfigFiles.GetStart(commandLine.Positional[0], Environment.CurrentDirectory);
            var path = ConfigFiles.Find(commandLine.GetOption("--config"), "structure.jsonc", start);
            var options = path == null ? new StructureOptions() : StructureConfig.Parse(StyleCommand.ReadConfig(path));
            commandLine.ReadOptions(StructureConfig.Table, options);
            return options;
        }
    }
}
