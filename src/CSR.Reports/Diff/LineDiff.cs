using System.Text;

namespace CSR.Reports.Diff
{
    // Unified diff двух текстов построчно (пути a/<путь>, b/<путь>, 3 строки контекста).
    // Общие начало и конец отбрасываются, середина - наибольшая общая подпоследовательность строк;
    // слишком большая середина - заменой целиком (дифф верный, но грубый)
    public static class LineDiff
    {
        private const int Context = 3;
        private const long MaxTable = 16_000_000;

        public static string Build(string path, string before, string after)
        {
            if (before == after)
                return "";

            var oldLines = Split(before);
            var newLines = Split(after);
            var edits = GetEdits(oldLines, newLines);
            var output = new StringBuilder();
            output.Append($"--- a/{path}\n+++ b/{path}\n");
            var index = 0;
            while (index < edits.Count)
            {
                if (edits[index].Kind == ' ')
                {
                    index++;
                    continue;
                }

                index = AppendHunk(output, edits, index);
            }

            return output.ToString();
        }

        // Строки без переводов; перевод в конце файла не дает пустой строки
        private static string[] Split(string text)
        {
            if (text.Length == 0)
                return [];

            var lines = text.Replace("\r\n", "\n").Split('\n');
            return text.EndsWith("\n", StringComparison.Ordinal) ? lines.Take(lines.Length - 1).ToArray() : lines;
        }

        private static List<Edit> GetEdits(string[] oldLines, string[] newLines)
        {
            var prefix = 0;
            while (prefix < oldLines.Length && prefix < newLines.Length && oldLines[prefix] == newLines[prefix])
                prefix++;

            var suffix = 0;
            while (suffix < oldLines.Length - prefix && suffix < newLines.Length - prefix
                && oldLines[oldLines.Length - 1 - suffix] == newLines[newLines.Length - 1 - suffix])
                suffix++;

            var edits = new List<Edit>();
            for (var i = 0; i < prefix; i++)
                edits.Add(new Edit(' ', i, i, oldLines[i]));

            AddMiddle(edits, oldLines, newLines, prefix, oldLines.Length - suffix, newLines.Length - suffix);
            for (var i = suffix; i > 0; i--)
            {
                var oldIndex = oldLines.Length - i;
                edits.Add(new Edit(' ', oldIndex, newLines.Length - i, oldLines[oldIndex]));
            }

            return edits;
        }

        private static void AddMiddle(List<Edit> edits, string[] oldLines, string[] newLines, int start, int oldEnd,
            int newEnd)
        {
            var oldCount = oldEnd - start;
            var newCount = newEnd - start;
            if ((long)(oldCount + 1) * (newCount + 1) > MaxTable)
            {
                for (var i = start; i < oldEnd; i++)
                    edits.Add(new Edit('-', i, -1, oldLines[i]));

                for (var j = start; j < newEnd; j++)
                    edits.Add(new Edit('+', -1, j, newLines[j]));

                return;
            }

            // lengths[i, j] - длина общей подпоследовательности хвостов с i-й и j-й строки середины
            var lengths = new int[oldCount + 1, newCount + 1];
            for (var i = oldCount - 1; i >= 0; i--)
            {
                for (var j = newCount - 1; j >= 0; j--)
                {
                    lengths[i, j] = oldLines[start + i] == newLines[start + j]
                        ? lengths[i + 1, j + 1] + 1
                        : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
                }
            }

            int oldIndex = 0, newIndex = 0;
            while (oldIndex < oldCount || newIndex < newCount)
            {
                bool bothLeft = oldIndex < oldCount && newIndex < newCount;
                if (bothLeft && oldLines[start + oldIndex] == newLines[start + newIndex])
                {
                    edits.Add(new Edit(' ', start + oldIndex, start + newIndex, oldLines[start + oldIndex]));
                    oldIndex++;
                    newIndex++;
                }
                else if (newIndex == newCount
                    || oldIndex < oldCount && lengths[oldIndex + 1, newIndex] >= lengths[oldIndex, newIndex + 1])
                {
                    edits.Add(new Edit('-', start + oldIndex, -1, oldLines[start + oldIndex]));
                    oldIndex++;
                }
                else
                {
                    edits.Add(new Edit('+', -1, start + newIndex, newLines[start + newIndex]));
                    newIndex++;
                }
            }
        }

        // Ханк от изменения index: контекст до, изменения, разделенные не больше чем 2 * Context общими строками,
        // контекст после. Возвращает индекс после ханка
        private static int AppendHunk(StringBuilder output, List<Edit> edits, int index)
        {
            var first = Math.Max(0, index - Context);
            var last = index;
            var equalRun = 0;
            for (var i = index; i < edits.Count; i++)
            {
                if (edits[i].Kind != ' ')
                {
                    last = i;
                    equalRun = 0;
                    continue;
                }

                equalRun++;
                if (equalRun > 2 * Context)
                    break;
            }

            var end = Math.Min(edits.Count, last + Context + 1);
            var oldStart = FindStart(edits, first, end, true);
            var newStart = FindStart(edits, first, end, false);
            var oldCount = edits.Skip(first).Take(end - first).Count(edit => edit.Kind != '+');
            var newCount = edits.Skip(first).Take(end - first).Count(edit => edit.Kind != '-');
            output.Append($"@@ -{FormatRange(oldStart, oldCount)} +{FormatRange(newStart, newCount)} @@\n");
            for (var i = first; i < end; i++)
                output.Append(edits[i].Kind).Append(edits[i].Text).Append('\n');

            return end;
        }

        // Номер первой строки ханка в старом или новом тексте (с 0); у ханка без строк - строка перед ним
        private static int FindStart(List<Edit> edits, int first, int end, bool old)
        {
            for (var i = first; i < end; i++)
            {
                var line = old ? edits[i].OldLine : edits[i].NewLine;
                if (line >= 0)
                    return line;
            }

            for (var i = first - 1; i >= 0; i--)
            {
                var line = old ? edits[i].OldLine : edits[i].NewLine;
                if (line >= 0)
                    return line + 1;
            }

            return 0;
        }

        private static string FormatRange(int start, int count)
            => count == 0 ? $"{start},0" : $"{start + 1},{count}";

        private record struct Edit(char Kind, int OldLine, int NewLine, string Text);
    }
}
