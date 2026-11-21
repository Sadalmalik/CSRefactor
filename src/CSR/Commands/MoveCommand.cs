using CSR.Workspace.Refactor;
using CSR.Workspace.Symbols;
using Microsoft.CodeAnalysis;

namespace CSR.Commands
{
    // csr move: перенос типа в свой файл. По умолчанию - дифф на stdout без записи; запись - --apply
    public static class MoveCommand
    {
        public const string Usage = """
            csr move <тип> [--to <файл.cs>] [опции]

              тип                        как в csr refs: Namespace.Type или файл.cs:строка[:колонка]
              --to <файл.cs>             файл назначения (от текущей папки); по умолчанию - <Тип>.cs рядом
                                         с исходным файлом
              --solution <файл>          .sln, .slnx или .csproj (по умолчанию - ближайшее решение вверх по папкам)
              --format diff | json       diff (по умолчанию): дифф в stdout, сводка в stderr; json - все в stdout
              --apply                    записать файлы (без него ничего не пишется)
              --force                    с --apply: записать при новых ошибках компиляции
              --no-verify                не компилировать затронутые проекты до и после
              --no-server                не обращаться к csr serve, загрузить решение в процессе

            Тип не один в файле: новый файл с теми же пространствами имен и нужными типу директивами using;
            в исходном убираются строки типа и ставшие лишними using. Тип один в файле: файл переносится
            целиком, .meta Unity - вместе с ним (GUID сохраняется). Текст типа не переформатируется.
            Отказ: вложенный тип, partial-тип в нескольких файлах, тип из метаданных, файл назначения уже
            есть или вне папки проекта, тип уже в файле со своим именем.
            Коды возврата: 0 - дифф готов или записан, 1 - отказ, новые ошибки компиляции или запись
            не выполнена, 2 - ошибка запуска
            """;

        private static readonly HashSet<string> OptionNames = ["--solution", "--format", "--to"];
        private static readonly HashSet<string> FlagNames = ["--no-server", "--apply", "--force", "--no-verify"];

        public static int Run(IEnumerable<string> args)
            => WorkspaceCommands.Run("move", args, OptionNames, FlagNames);

        public static async Task<int> ExecuteAsync(
            WorkspaceContext context,
            string[] args,
            CancellationToken cancellation)
        {
            var commandLine = CommandLine.Parse(args, OptionNames, FlagNames);
            if (commandLine.Positional.Count != 1)
                throw new UsageException("move: ожидается один символ");

            var format = RefactorOutput.GetFormat(commandLine);
            var query = SymbolQuery.Parse(commandLine.Positional[0]);
            var directory = context.WorkingDirectory;
            var symbols = await SymbolResolver.ResolveAsync(context.Solution, query, directory, false, cancellation);
            if (symbols[0] is not INamedTypeSymbol type)
                throw new UsageException($"move: {SymbolText.Signature(symbols[0])} - не тип");

            var plan = await TypeMover.PlanAsync(
                context.Solution,
                type,
                commandLine.GetOption("--to"),
                directory,
                !commandLine.HasFlag("--no-verify"),
                cancellation);
            return RefactorOutput.Finish(context, plan, commandLine, format);
        }
    }
}
