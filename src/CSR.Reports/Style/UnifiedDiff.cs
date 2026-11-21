using System.Text.RegularExpressions;
using CSR.Core.Style;

namespace CSR.Reports.Style
{
    // Измененные строки из unified diff (git diff -U0): путь из "+++ b/<путь>", строки из "@@ -a,b +c,d @@"
    public static class UnifiedDiff
    {
        private static readonly Regex Hunk = new(@"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@",
            RegexOptions.CultureInvariant);

        // Ключ - путь из диффа (относительно корня репозитория, разделитель '/')
        public static Dictionary<string, ChangedLines> Parse(string diff)
        {
            var files = new Dictionary<string, ChangedLines>(StringComparer.OrdinalIgnoreCase);
            ChangedLines? current = null;
            foreach (var rawLine in diff.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.StartsWith("+++ ", StringComparison.Ordinal))
                {
                    var path = line.Substring(4).Trim();
                    current = null;
                    if (path == "/dev/null")
                        continue;

                    if (path.StartsWith("b/", StringComparison.Ordinal))
                        path = path.Substring(2);

                    current = new ChangedLines();
                    files[path] = current;
                    continue;
                }

                var match = Hunk.Match(line);
                if (current == null || !match.Success)
                    continue;

                var start = int.Parse(match.Groups[1].Value);
                var count = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 1;
                current.Add(start, count);
            }

            return files;
        }
    }
}
