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
            {
                if (FileScope.IsInScope(configuration, tree.FilePath))
                    configuration.Check.Analyze(new CheckContext(tree, project, configuration, findings));
            }

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

        // Строка (с 1) изменена
        public bool Contains(int line)
            => IsWholeFile || m_Lines.Contains(line);

        public bool Intersects(Finding finding)
        {
            for (int line = finding.Line; line <= finding.EndLine; line++)
            {
                if (Contains(line))
                    return true;
            }

            return false;
        }
    }
}
