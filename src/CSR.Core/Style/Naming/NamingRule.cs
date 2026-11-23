using CSR.Core.Checks;

namespace CSR.Core.Style.Naming
{
    // Правило именования: какие символы, требования по ключу вида (null - вид занят без проверки, поле skip),
    // уровень и метка (null - проверки), подлежащее сообщения (null - вид символа: "закрытое поле")
    public class NamingRule
    {
        public static readonly IReadOnlyList<string> OwnFields = ["symbols", "skip", "severity", "rule", "subject"];

        public NamingRule(
            NameSelector symbols,
            IReadOnlyDictionary<string, INameRequirement?> requirements,
            Severity? severity = null,
            string? rule = null,
            string? subject = null)
        {
            Symbols = symbols;
            Requirements = requirements;
            Severity = severity;
            Rule = rule;
            Subject = subject;
        }

        public NameSelector Symbols { get; }

        // Ключ вида (каскад) -> требование; null - вид занят без проверки
        public IReadOnlyDictionary<string, INameRequirement?> Requirements { get; }

        public Severity? Severity { get; }

        public string? Rule { get; }

        public string? Subject { get; }

        // Запись конфига -> правило; поля требований - из видов реестра
        public static NamingRule Parse(Record record)
        {
            record.RequireKnown(OwnFields.Concat(NamingCatalog.Kinds.SelectMany(kind => kind.Fields)));
            var symbols = NameSelector.Parse(record.GetRecord("symbols"));
            var severityKey = record.GetChoice("severity", "error", "warning", "hint");
            var severity = severityKey == null ? (Severity?)null : Finding.ParseSeverity(severityKey);
            var requirements = new Dictionary<string, INameRequirement?>();
            foreach (var key in record.GetStrings("skip", NamingCatalog.Kinds.Select(kind => kind.Key).ToList()))
                requirements[key] = null;

            foreach (var kind in NamingCatalog.Kinds)
            {
                var requirement = kind.Create(record);
                if (requirement == null)
                    continue;

                if (requirements.ContainsKey(kind.Key))
                    throw new FormatException($"skip: вид '{kind.Key}' и задан, и пропущен");

                requirements[kind.Key] = requirement;
            }

            if (requirements.Count == 0)
                throw new FormatException("в правиле нет требования к имени");

            return new NamingRule(symbols, requirements, severity, record.GetString("rule"), record.GetString("subject"));
        }
    }
}
