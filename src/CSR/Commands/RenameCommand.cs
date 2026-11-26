using CSR.Workspace.Rename;
using CSR.Workspace.Symbols;

namespace CSR.Commands
{
    // csr rename: семантическое переименование. По умолчанию - дифф на stdout без записи; запись - --apply
    public static class RenameCommand
    {
        public const string Usage = """
            csr rename <символ> <новое имя> [опции]

              символ                     как в csr refs: Namespace.Type.Member или файл.cs:строка[:колонка]
              --solution <файл>          .sln, .slnx или .csproj (по умолчанию - ближайшее решение вверх по папкам)
              --config <refactor.jsonc>  атрибуты сериализации проекта, где и что искать в строках; без --config -
                                         .csr/refactor.jsonc от папки решения вверх, иначе config/ рядом с csr.exe
              --format diff | json       diff (по умолчанию): дифф в stdout, сводка в stderr; json - все в stdout
              --apply                    записать файлы (без него ничего не пишется)
              --migrate                  сохранить старое имя для данных: [FormerlySerializedAs] (Unity),
                                         имя в [JsonProperty] / [DataMember]
              --force                    с --apply: записать при строковых совпадениях и новых ошибках компиляции
              --no-verify                не компилировать затронутые проекты до и после
              --no-strings               не искать строковые совпадения
              --no-server                не обращаться к csr serve, загрузить решение в процессе

            Отказ без --migrate: поле, которое сериализует Unity ([SerializeField], [SerializeReference],
            публичное поле [Serializable]-типа или MonoBehaviour / ScriptableObject), [JsonProperty] и
            [DataMember] без имени, атрибуты из конфига; сообщения Unity (Update, OnEnable...) - всегда.
            Строковые совпадения (литералы C#, Lua, JSON, YAML-ассеты; nameof не в счет) - список
            "проверить вручную"; переименование их не меняет.
            Коды возврата: 0 - дифф готов или записан, 1 - отказ, новые ошибки компиляции или запись
            не выполнена, 2 - ошибка запуска
            """;

        private static readonly HashSet<string> OptionNames = ["--solution", "--format", "--config"];
        private static readonly HashSet<string> FlagNames =
            ["--no-server", "--apply", "--migrate", "--force", "--no-verify", "--no-strings"];

        public static int Run(IEnumerable<string> args)
            => WorkspaceCommandTools.Run("rename", args, OptionNames, FlagNames, Validate);

        private static void Validate(CommandLine commandLine)
        {
            if (commandLine.Positional.Count != 2)
                throw new UsageException("rename: ожидаются символ и новое имя");

            var format = commandLine.GetOption("--format") ?? "diff";
            if (format is not ("diff" or "json"))
                throw new UsageException($"неизвестный формат '{format}'");

            WorkspaceCommandTools.ValidateSymbol(commandLine.Positional[0]);
        }

        public static async Task<int> ExecuteAsync(
            WorkspaceContext context,
            string[] args,
            CancellationToken cancellation)
        {
            var commandLine = CommandLine.Parse(args, OptionNames, FlagNames);
            Validate(commandLine);
            var format = commandLine.GetOption("--format") ?? "diff";

            var options = context.CreateConfigs(commandLine).LoadRefactor();
            var settings = new RenameSettings
            {
                Migrate = commandLine.HasFlag("--migrate"),
                Verify = !commandLine.HasFlag("--no-verify"),
                ScanStrings = !commandLine.HasFlag("--no-strings"),
            };
            var loaded = context.Solution;
            var query = SymbolQuery.Parse(commandLine.Positional[0]);
            var directory = context.WorkingDirectory;
            var symbols = await SymbolResolver.ResolveAsync(loaded, query, directory, false, cancellation);
            var plan = await RenameEngine.PlanAsync(
                loaded,
                symbols[0],
                commandLine.Positional[1],
                settings,
                options,
                cancellation);
            var status = GetStatus(plan, commandLine.HasFlag("--apply"), commandLine.HasFlag("--force"));
            if (status == RenameStatus.Applied)
                RenameEngine.Apply(loaded, plan);

            if (format == "json")
            {
                context.Output.Write(RenameReport.WriteJson(plan, status));
            }
            else
            {
                context.Output.Write(plan.Diff);
                context.Error.Write(RenameReport.WriteSummary(plan, status));
            }

            return status is RenameStatus.Refused or RenameStatus.Blocked || plan.HasNewErrors ? 1 : 0;
        }

        private static RenameStatus GetStatus(RenamePlan plan, bool apply, bool force)
        {
            if (plan.SerializationRefused)
                return RenameStatus.Refused;

            if (!apply)
                return RenameStatus.Preview;

            if (!force && (plan.HasNewErrors || plan.HasStringMatches))
                return RenameStatus.Blocked;

            return RenameStatus.Applied;
        }
    }
}
