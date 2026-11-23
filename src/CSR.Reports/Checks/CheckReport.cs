using CSR.Core.Checks;

namespace CSR.Reports.Checks
{
    // Отчет проверок по синтаксису (style, arch): находки и режим
    public class CheckReport
    {
        public required int FileCount { get; init; }

        // file или changed-lines
        public required string Scope { get; init; }

        public required IReadOnlyList<Finding> Findings { get; init; }

        public bool HasErrors
            => Findings.Any(finding => finding.Severity == Severity.Error);

        // Сводка по проверкам и их правилам (у naming уровень и метка - у правила): сначала уровень выше, затем имя
        public List<CheckSummary> Summarize()
        {
            return Findings
                .GroupBy(finding => (finding.Check, finding.Rule, finding.Severity))
                .Select(group => new CheckSummary
                {
                    Check = group.Key.Check,
                    Rule = group.Key.Rule,
                    Severity = group.Key.Severity,
                    Count = group.Count(),
                })
                .OrderByDescending(summary => summary.Severity)
                .ThenBy(summary => summary.Check, StringComparer.Ordinal)
                .ToList();
        }
    }

    public class CheckSummary
    {
        public required string Check { get; init; }

        public string? Rule { get; init; }

        public required Severity Severity { get; init; }

        public required int Count { get; init; }
    }
}
