using System.Text.RegularExpressions;
using CSR.Workspace.Refactor;

namespace CSR.Commands
{
    // csr extract: извлечение метода из строк файла. По умолчанию - дифф на stdout без записи; запись - --apply
    public static class ExtractCommand
    {
        public const string Usage = """
            csr extract <файл.cs:строка-строка> <имя> [опции]

              файл.cs:строка-строка      строки (с 1, включительно): целые инструкции одного блока метода,
                                         конструктора или аксессора; путь - от текущей папки
              имя                        имя нового метода
              --solution <файл>          .sln, .slnx или .csproj (по умолчанию - ближайшее решение вверх по папкам)
              --format diff | json       diff (по умолчанию): дифф в stdout, сводка в stderr; json - все в stdout
              --apply                    записать файлы (без него ничего не пишется)
              --force                    с --apply: записать при новых ошибках компиляции
              --no-verify                не компилировать затронутые проекты до и после
              --no-server                не обращаться к csr serve, загрузить решение в процессе

            Входы строк - параметры, один выход - возвращаемое значение, несколько выходов - out-параметры
            (объявленный в строках выход - out var в месте вызова). Метод private, static - если не нужен экземпляр; ставится после члена,
            строки сдвигаются на отступ тела без переформатирования.
            Отказ: переменная читается и меняется (нужен ref), return / break / continue / goto из строк,
            await, yield, строки в лямбде или локальной функции, тип из параметров типа обобщенного метода,
            анонимный тип, многострочная verbatim-строка.
            Коды возврата: 0 - дифф готов или записан, 1 - отказ, новые ошибки компиляции или запись
            не выполнена, 2 - ошибка запуска
            """;

        private static readonly HashSet<string> OptionNames = ["--solution", "--format"];
        private static readonly HashSet<string> FlagNames = ["--no-server", "--apply", "--force", "--no-verify"];
        private static readonly Regex RangePattern = new(@"^(?<file>.+):(?<start>\d+)-(?<end>\d+)$");

        public static int Run(IEnumerable<string> args)
            => WorkspaceCommands.Run("extract", args, OptionNames, FlagNames);

        public static async Task<int> ExecuteAsync(
            WorkspaceContext context,
            string[] args,
            CancellationToken cancellation)
        {
            var commandLine = CommandLine.Parse(args, OptionNames, FlagNames);
            if (commandLine.Positional.Count != 2)
                throw new UsageException("extract: ожидаются строки файла и имя метода");

            var format = RefactorOutput.GetFormat(commandLine);
            var range = RangePattern.Match(commandLine.Positional[0]);
            if (!range.Success)
                throw new UsageException(
                    $"extract: строки задаются как файл.cs:строка-строка, получено '{commandLine.Positional[0]}'");

            var path = Path.GetFullPath(Path.Combine(context.WorkingDirectory, range.Groups["file"].Value));
            var plan = await MethodExtractor.PlanAsync(
                context.Solution,
                path,
                int.Parse(range.Groups["start"].Value),
                int.Parse(range.Groups["end"].Value),
                commandLine.Positional[1],
                !commandLine.HasFlag("--no-verify"),
                cancellation);
            return RefactorOutput.Finish(context, plan, commandLine, format);
        }
    }
}
