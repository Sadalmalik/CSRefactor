using CSR.Core.Structure;
using CSR.Reports.Config;
using CSR.Reports.Structure;

namespace CSR.Commands
{
    // csr structure: структурные метрики на графе типов - глубина владения и наследования, объекты-боги, циклы и
    // свалки папок и пространств имен, концентраторы, размер сборок, глубина слоя. Ничего не пишет рядом с кодом
    public static class StructureCommand
    {
        public const string Usage = """
            csr structure <файлы | папки>... [опции]

              --config <structure.jsonc>   пороги отметок и размер топов; без --config - .csr/structure.jsonc в папке
                                           цели или выше, иначе config/structure.jsonc рядом с csr.exe, иначе по умолчанию
              --project <project.jsonc>    символы препроцессора; поиск - как у style
              --top <N>                    размер топов (по умолчанию 10); 0 - все
              --format json | md           формат отчета (по умолчанию json)
              --out <файл>                 записать отчет в файл вместо stdout

            Граф типов строится на компиляции файлов цели без загрузки решения: типы сторонних библиотек не разрешены.
            Сборка файла - ближайшая папка вверх с *.asmdef или *.csproj; размер сборки - строки кода ее файлов в цели.
            Коды возврата: 0 - успех (отметки ошибкой не считаются), 2 - ошибка запуска
            """;

        private static readonly FileCommand Command = new()
        {
            Name = "structure",
            Options = StructureConfig.Table.OptionNames.ToList(),
            Analyze = Analyze,
        };

        public static int Run(IEnumerable<string> args)
            => FileCommands.Run(Command, args);

        private static FileResult Analyze(FileRun run)
        {
            var config = run.Configs.Read("structure.jsonc");
            var options = config == null ? new StructureOptions() : StructureConfig.Parse(config);
            run.CommandLine.ReadOptions(StructureConfig.Table, options);

            var source = SourceGraph.Build(SourceGraph.Parse(run.Files, run.Project));
            var result = StructureAnalysis.Run(
                source.Graph,
                [source.Compilation],
                source.Trees,
                new AssemblyFiles(source.Trees.Select(tree => tree.FilePath)).Get,
                options,
                CancellationToken.None);
            var report = new StructureReport { FileCount = run.Files.Count, Options = options, Result = result };
            return new FileResult
            {
                Text = run.Format == "md" ? StructureMarkdownWriter.Write(report) : StructureJsonWriter.Write(report),
                Summary = $"файлов {run.Files.Count}, типов {result.Graph.Nodes.Count}, ребер {result.Graph.EdgeCount}",
            };
        }
    }
}
