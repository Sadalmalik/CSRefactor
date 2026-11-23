using CSR.Core.Checks;

namespace CSR.Core.Style.Naming
{
    // Именование по правилам конфига: правило - какие символы, требование к имени, уровень и метка. Каскад: правила
    // по порядку, для каждого вида требования (ключа) действует первое подходящее правило, которое его задает
    public class NamingCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Records("rules", "правила: symbols (селектор), требования, severity, rule", NamingRule.Parse),
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

            var taken = new HashSet<string>();
            foreach (var symbol in NameSymbols.Collect(context.Root))
            {
                taken.Clear();
                foreach (var rule in rules)
                {
                    if (!rule.Symbols.IsMatch(symbol))
                        continue;

                    var subject = rule.Label == null ? symbol : symbol with { Label = rule.Label };
                    foreach (var requirement in rule.Requirements)
                    {
                        if (!taken.Add(requirement.Key))
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
