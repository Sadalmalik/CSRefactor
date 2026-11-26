using CSR.Core.Checks;

namespace CSR.Core.Style.Naming
{
    // Именование по правилам конфига: правило - какие символы, требование к имени, уровень и метка. Каскад: правила
    // по порядку, для каждого вида требования (ключа) действует первое подходящее правило, которое его задает
    public class NamingCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Records(
                "rules",
                "правила: symbols (селектор), требования, skip, severity, rule, subject",
                NamingRule.Parse),
        ];

        public override string Name
            => "naming";

        public override string Description
            => "имена по правилам: символы -> требование к имени";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
            => Run(context, context.Settings.GetRecords<NamingRule>("rules"));

        // Правила к именам файла; находка - с уровнем и меткой правила, без них - проверки
        public static void Run(CheckContext context, IReadOnlyList<NamingRule> rules)
        {
            if (rules.Count == 0)
                return;

            var kinds = rules.Any(rule => rule.Symbols.Kinds.Count == 0)
                ? null
                : new HashSet<string>(rules.SelectMany(rule => rule.Symbols.Kinds));
            var taken = new HashSet<string>();
            foreach (var symbol in NameSymbolTool.Collect(context.Root, kinds))
            {
                taken.Clear();
                foreach (var rule in rules)
                {
                    if (!rule.Symbols.IsMatch(symbol))
                        continue;

                    var subject = rule.Subject == null ? symbol : symbol with { Label = rule.Subject };
                    foreach (var requirement in rule.Requirements)
                    {
                        if (!taken.Add(requirement.Key) || requirement.Value == null)
                            continue;

                        var problem = requirement.Value.Check(subject);
                        if (problem != null)
                            context.Report(symbol.Span, problem, rule.Severity ?? context.Severity, rule.Rule ?? context.Rule);
                    }
                }
            }
        }
    }
}
