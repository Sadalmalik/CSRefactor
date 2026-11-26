using CSR.Workspace.Refactor;
using CSR.Workspace.Symbols;
using Microsoft.CodeAnalysis;

namespace CSR.Commands
{
    // csr move: перенос типа в свой файл или члена в другой тип. По умолчанию - дифф на stdout без записи;
    // запись - --apply
    public static class MoveCommand
    {
        public const string Usage = """
            csr move <тип> [--to <файл.cs>] [опции]
            csr move <член> --to <тип> [опции]

              тип, член                  как в csr refs: Namespace.Type[.Member] или файл.cs:строка[:колонка]
              --to <файл.cs>             для типа: файл назначения (от текущей папки); по умолчанию - <Тип>.cs
                                         рядом с исходным файлом
              --to <тип>                 для члена: тип-приемник, Namespace.Type
              --config <refactor.jsonc>  атрибуты сериализации проекта (как у csr rename)
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
            Член: статический метод, свойство, поле, константа - ссылки получают квалификатор приемника;
            метод экземпляра - только с параметром типа-приемника: вызов M(a, p) становится p.M(a).
            Отказ: поле или свойство экземпляра, сериализуемые данные, override / virtual / реализация
            интерфейса, член использует экземпляр или private-члены исходного типа, private-член
            используется вне себя, в приемнике уже есть член с этим именем, ссылка в сгенерированном коде.
            Коды возврата: 0 - дифф готов или записан, 1 - отказ, новые ошибки компиляции или запись
            не выполнена, 2 - ошибка запуска
            """;

        private static readonly HashSet<string> OptionNames = ["--solution", "--format", "--to", "--config"];
        private static readonly HashSet<string> FlagNames = ["--no-server", "--apply", "--force", "--no-verify"];

        public static int Run(IEnumerable<string> args)
            => WorkspaceCommands.Run("move", args, OptionNames, FlagNames, Validate);

        private static void Validate(CommandLine commandLine)
        {
            if (commandLine.Positional.Count != 1)
                throw new UsageException("move: ожидается один символ");

            RefactorOutput.GetFormat(commandLine);
            WorkspaceCommands.ValidateSymbol(commandLine.Positional[0]);
        }

        public static async Task<int> ExecuteAsync(
            WorkspaceContext context,
            string[] args,
            CancellationToken cancellation)
        {
            var commandLine = CommandLine.Parse(args, OptionNames, FlagNames);
            Validate(commandLine);
            var format = RefactorOutput.GetFormat(commandLine);
            var query = SymbolQuery.Parse(commandLine.Positional[0]);
            var directory = context.WorkingDirectory;
            var symbols = await SymbolResolver.ResolveAsync(context.Solution, query, directory, false, cancellation);
            var to = commandLine.GetOption("--to");
            bool verify = !commandLine.HasFlag("--no-verify");
            if (symbols[0] is INamedTypeSymbol type)
            {
                var typePlan = await TypeMover.PlanAsync(context.Solution, type, to, directory, verify, cancellation);
                return RefactorOutput.Finish(context, typePlan, commandLine, format);
            }

            if (to == null)
                throw new UsageException("move: для члена нужен --to <тип-приемник>");

            var targets = await SymbolResolver.ResolveAsync(
                context.Solution,
                SymbolQuery.Parse(to),
                directory,
                false,
                cancellation);
            if (targets[0] is not INamedTypeSymbol target)
                throw new UsageException($"move: --to {to} - не тип");

            var options = RenameCommand.LoadOptions(commandLine, context);
            var plan = await MemberMover.PlanAsync(context.Solution, symbols[0], target, options, verify, cancellation);
            return RefactorOutput.Finish(context, plan, commandLine, format);
        }
    }
}
