using CSR.Core.Arch;
using CSR.Core.Checks;

namespace CSR.Core.Style.Forbidden
{
    // Запись forbidden: вид (call, type), маска имени, чем заменить, где не действует, уровень и метка (null - проверки)
    public class ForbiddenEntry
    {
        private static readonly IReadOnlyList<string> Fields = ["kind", "mask", "use", "excludePaths", "severity", "rule"];

        private readonly NameMask m_Mask;
        private readonly NameMask m_ExcludePaths;

        public ForbiddenEntry(
            string kind,
            string mask,
            string use,
            IReadOnlyList<string> excludePaths,
            Severity? severity = null,
            string? rule = null)
        {
            Kind = kind;
            Mask = mask;
            Use = use;
            m_Mask = NameMask.Get([mask]);
            m_ExcludePaths = NameMask.Get(excludePaths, true);
            Severity = severity;
            Rule = rule;
        }

        public string Kind { get; }

        public string Mask { get; }

        public string Use { get; }

        public Severity? Severity { get; }

        public string? Rule { get; }

        public bool IsMatch(string name)
            => m_Mask.IsMatch(name);

        public bool IsExcluded(string path)
            => FileScope.IsExcluded(m_ExcludePaths, path);

        public static ForbiddenEntry Parse(Record record)
        {
            record.RequireKnown(Fields);
            var kind = record.GetChoice("kind", ForbiddenKinds.Call, ForbiddenKinds.Type);
            if (kind == null)
                throw new FormatException("kind: нужно call или type");

            var mask = record.GetString("mask");
            if (mask == null)
                throw new FormatException("mask: нужна маска имени");

            var severityKey = record.GetChoice("severity", "error", "warning", "hint");
            var severity = severityKey == null ? (Severity?)null : Finding.ParseSeverity(severityKey);
            return new ForbiddenEntry(
                kind,
                mask,
                record.GetString("use") ?? "",
                record.GetStrings("excludePaths"),
                severity,
                record.GetString("rule"));
        }
    }
}
