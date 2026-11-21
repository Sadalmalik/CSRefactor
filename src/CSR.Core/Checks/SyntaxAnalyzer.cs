using CSR.Core.Config;
using Microsoft.CodeAnalysis;

namespace CSR.Core.Checks
{
    public static class SyntaxAnalyzer
    {
        public static List<Finding> Analyze(SyntaxTree tree, CheckOptions options, ProjectOptions project)
        {
            var findings = new List<Finding>();
            foreach (var configuration in options.Checks)
                configuration.Check.Analyze(new CheckContext(tree, project, configuration, findings));

            return findings
                .OrderBy(finding => finding.Line)
                .ThenBy(finding => finding.Column)
                .ThenBy(finding => finding.Check, StringComparer.Ordinal)
                .ToList();
        }
    }

    // Измененные строки файла (с 1) для режима changed-lines
    public class ChangedLines
    {
        private readonly HashSet<int> m_Lines = new();

        public bool IsWholeFile { get; init; }

        public void Add(int first, int count)
        {
            for (int line = first; line < first + count; line++)
                m_Lines.Add(line);
        }

        public bool Intersects(Finding finding)
        {
            if (IsWholeFile)
                return true;

            for (int line = finding.Line; line <= finding.EndLine; line++)
            {
                if (m_Lines.Contains(line))
                    return true;
            }

            return false;
        }
    }
}
