using CSR.Core.Checks;

namespace CSR.Core.Style.Forbidden
{
    // Запрещенное API по записям конфига: вид (обращение Тип.Член или тип), маска имени, чем заменить. На каждое
    // обращение - первая подходящая запись его вида, файл которой не исключен excludePaths записи
    public class ForbiddenCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Records(
                "entries",
                "записи: kind (call, type), mask, use, excludePaths, severity, rule",
                ForbiddenEntry.Parse),
        ];

        public override string Name
            => "forbidden";

        public override string Description
            => "запрещенные вызовы, члены и типы";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
            => Run(context, context.Settings.GetRecords<ForbiddenEntry>("entries"));

        public static void Run(CheckContext context, IReadOnlyList<ForbiddenEntry> entries)
        {
            var active = entries.Where(entry => !entry.IsExcluded(context.Tree.FilePath)).ToList();
            foreach (var kind in ForbiddenKinds.All)
            {
                var kindEntries = active.Where(entry => entry.Kind == kind).ToList();
                if (kindEntries.Count == 0)
                    continue;

                foreach (var use in ForbiddenKinds.Find(kind, context.Root))
                {
                    var entry = kindEntries.FirstOrDefault(candidate => candidate.IsMatch(use.Name));
                    if (entry == null)
                        continue;

                    var message = ForbiddenNames.WithUse($"{ForbiddenKinds.GetSubject(kind)} '{use.Name}'", entry.Use);
                    context.Report(use.Span, message, entry.Severity ?? context.Severity, entry.Rule ?? context.Rule);
                }
            }
        }
    }
}
