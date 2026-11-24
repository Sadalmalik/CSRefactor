using System.Text.RegularExpressions;
using CSR.Core.Checks;

namespace CSR.Reports.Style
{
    // Измененные строки из unified diff (git diff, с контекстом или без): путь из "+++ b/<путь>", строки - '+' в теле ханка
    public static class UnifiedDiff
    {
        private static readonly Regex Hunk = new(
            @"^@@ -\d+(?:,(\d+))? \+(\d+)(?:,(\d+))? @@",
            RegexOptions.CultureInvariant);

        // Ключ - путь из диффа (относительно корня репозитория, разделитель '/')
        public static Dictionary<string, ChangedLines> Parse(string diff)
        {
            var files = new Dictionary<string, ChangedLines>(StringComparer.OrdinalIgnoreCase);
            ChangedLines? current = null;
            var body = new HunkBody();
            foreach (var rawLine in diff.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (body.IsOpen && body.Read(line))
                    continue;

                if (line.StartsWith("+++ ", StringComparison.Ordinal))
                {
                    current = StartFile(files, line.Substring(4).Trim());
                    continue;
                }

                var match = Hunk.Match(line);
                if (current != null && match.Success)
                    body = HunkBody.Start(match, current);
            }

            return files;
        }

        private static ChangedLines? StartFile(Dictionary<string, ChangedLines> files, string path)
        {
            if (path == "/dev/null")
                return null;

            if (path.StartsWith("b/", StringComparison.Ordinal))
                path = path.Substring(2);

            var lines = new ChangedLines();
            files[path] = lines;
            return lines;
        }

        // Тело ханка после "@@": добавленные строки - в измененные строки файла
        private struct HunkBody
        {
            private ChangedLines m_Lines;
            private int m_OldLeft;
            private int m_NewLeft;
            private int m_NewLine;

            public readonly bool IsOpen
                => m_OldLeft > 0 || m_NewLeft > 0;

            public static HunkBody Start(Match match, ChangedLines lines)
            {
                return new HunkBody
                {
                    m_Lines = lines,
                    m_OldLeft = ReadCount(match.Groups[1]),
                    m_NewLine = int.Parse(match.Groups[2].Value),
                    m_NewLeft = ReadCount(match.Groups[3]),
                };
            }

            private static int ReadCount(Group group)
                => group.Success ? int.Parse(group.Value) : 1;

            // false - строка не из тела (дифф обрезан): тело закрыто, строку разбирают как заголовок
            public bool Read(string line)
            {
                var kind = line.Length == 0 ? ' ' : line[0];
                switch (kind)
                {
                    case '+':
                        m_Lines.Add(m_NewLine, 1);
                        m_NewLine++;
                        m_NewLeft--;
                        return true;
                    case '-':
                        m_OldLeft--;
                        return true;
                    case '\\':
                        return true;
                    case ' ':
                        m_NewLine++;
                        m_OldLeft--;
                        m_NewLeft--;
                        return true;
                    default:
                        m_OldLeft = 0;
                        m_NewLeft = 0;
                        return false;
                }
            }
        }
    }
}
