using System.Diagnostics;
using CSR.Workspace.References;
using CSR.Workspace.Symbols;

namespace CSR.Commands
{
    // csr refs: все ссылки на символ по решению. Ничего не пишет
    public static class RefsCommand
    {
        public const string Usage = """
            csr refs <символ> [опции]

              символ                     Namespace.Type.Member, Namespace.Type.Method(int, string)
                                         или файл.cs:строка[:колонка] (с 1; без колонки - первое объявление в строке)
              --solution <файл>          .sln, .slnx или .csproj (по умолчанию - ближайшее решение вверх по папкам)
              --format json | text       json (по умолчанию) или строки "файл:строка:колонка: код"
              --no-server                не обращаться к csr serve, загрузить решение в процессе

            Перегрузки метода без параметров в запросе - все. В ответе - сам символ и связанные:
            переопределения, реализации интерфейсов, аксессоры. Пути - от папки решения.
            Коды возврата: 0 - успех, 2 - ошибка запуска или символ не найден
            """;

        private static readonly HashSet<string> OptionNames = ["--solution", "--format"];
        private static readonly HashSet<string> FlagNames = ["--no-server"];

        public static int Run(IEnumerable<string> args)
            => WorkspaceCommands.Run("refs", args, OptionNames, FlagNames);

        public static async Task<int> ExecuteAsync(
            WorkspaceContext context,
            string[] args,
            CancellationToken cancellation)
        {
            var commandLine = CommandLine.Parse(args, OptionNames, FlagNames);
            if (commandLine.Positional.Count != 1)
                throw new UsageException("refs: ожидается один символ");

            var format = commandLine.GetOption("--format") ?? "json";
            if (format is not ("json" or "text"))
                throw new UsageException($"неизвестный формат '{format}'");

            var stopwatch = Stopwatch.StartNew();
            var query = SymbolQuery.Parse(commandLine.Positional[0]);
            var loaded = context.Solution;
            var directory = context.WorkingDirectory;
            var symbols = await SymbolResolver.ResolveAsync(loaded, query, directory, true, cancellation);
            var report = await ReferenceFinder.FindAsync(
                loaded.Current,
                loaded.Root,
                query.Text,
                symbols,
                cancellation);
            var text = format == "json" ? ReferenceWriter.WriteJson(report) : ReferenceWriter.WriteText(report);
            context.Output.Write(text);
            var seconds = stopwatch.Elapsed.TotalSeconds;
            var count = report.ReferenceCount;
            context.Error.WriteLine($"csr refs: ссылок {count} в {report.FileCount} файлах, {seconds:F1} с");
            return 0;
        }
    }
}
