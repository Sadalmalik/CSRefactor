using CSR.Core.Checks;

namespace CSR.Core.Style.Naming
{
    // Правило именования: какие символы, требования по ключу вида, уровень и метка (null - проверки)
    public class NamingRule
    {
        public static readonly IReadOnlyList<string> OwnFields = ["symbols", "severity", "rule"];

        public NamingRule(
            NameSelector symbols,
            IReadOnlyDictionary<string, INameRequirement> requirements,
            Severity? severity = null,
            string? rule = null)
        {
            Symbols = symbols;
            Requirements = requirements;
            Severity = severity;
            Rule = rule;
        }

        public NameSelector Symbols { get; }

        // Ключ вида (каскад) -> требование
        public IReadOnlyDictionary<string, INameRequirement> Requirements { get; }

        public Severity? Severity { get; }

        public string? Rule { get; }

        // Запись конфига -> правило; поля требований - из видов реестра
        public static NamingRule Parse(Record record)
        {
            record.RequireKnown(OwnFields.Concat(NamingCatalog.Kinds.SelectMany(kind => kind.Fields)));
            var symbols = NameSelector.Parse(record.GetRecord("symbols"));
            var severityKey = record.GetChoice("severity", "error", "warning", "hint");
            var severity = severityKey == null ? (Severity?)null : Finding.ParseSeverity(severityKey);
            var requirements = new Dictionary<string, INameRequirement>();
            foreach (var kind in NamingCatalog.Kinds)
            {
                var requirement = kind.Create(record);
                if (requirement != null)
                    requirements[kind.Key] = requirement;
            }

            if (requirements.Count == 0)
                throw new FormatException("в правиле нет требования к имени");

            return new NamingRule(symbols, requirements, severity, record.GetString("rule"));
        }
    }
}
