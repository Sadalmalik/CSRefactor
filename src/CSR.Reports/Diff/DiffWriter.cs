using System.Text;

namespace CSR.Reports.Diff
{
    // Строка текста для диффа; HasEnding = false - последняя строка файла без перевода строки
    public readonly record struct DiffLine(string Text, bool HasEnding);

    // Строки [OldStart, OldEnd) старого текста заменены строками [NewStart, NewEnd) нового; строки между блоками совпадают
    public readonly record struct DiffBlock(int OldStart, int OldEnd, int NewStart, int NewEnd);

    // Unified diff по блокам правок, как git (3 строки контекста); середина блока больше MaxTable - удалением и вставкой целиком
    public static class DiffWriter
    {
        private const int Context = 3;
        private const long MaxTable = 16_000_000;
        private const string NoEnding = "\\ No newline at end of file\n";

        // oldName, newName - "a/<путь>", "b/<путь>" или "/dev/null"; "" - текст не изменился
        public static string Write(
            string oldName,
            string newName,
            IReadOnlyList<DiffLine> oldLines,
            IReadOnlyList<DiffLine> newLines,
            IReadOnlyList<DiffBlock> blocks)
        {
            var edits = GetEdits(oldLines, newLines, blocks);
            if (edits.All(edit => edit.Kind == ' '))
                return "";

            var output = new StringBuilder();
            output.Append($"--- {oldName}\n+++ {newName}\n");
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

        private static List<Edit> GetEdits(
            IReadOnlyList<DiffLine> oldLines,
            IReadOnlyList<DiffLine> newLines,
            IReadOnlyList<DiffBlock> blocks)
        {
            var edits = new List<Edit>();
            int oldLine = 0, newLine = 0;
            foreach (var block in blocks)
            {
                for (; oldLine < block.OldStart; oldLine++, newLine++)
                    edits.Add(new Edit(' ', oldLine, newLine, oldLines[oldLine]));

                AddBlock(edits, oldLines, newLines, block);
                oldLine = block.OldEnd;
                newLine = block.NewEnd;
            }

            for (; oldLine < oldLines.Count; oldLine++, newLine++)
                edits.Add(new Edit(' ', oldLine, newLine, oldLines[oldLine]));

            return edits;
        }

        private static void AddBlock(
            List<Edit> edits,
            IReadOnlyList<DiffLine> oldLines,
            IReadOnlyList<DiffLine> newLines,
            DiffBlock block)
        {
            int oldStart = block.OldStart, newStart = block.NewStart;
            int oldEnd = block.OldEnd, newEnd = block.NewEnd;
            while (oldStart < oldEnd && newStart < newEnd && oldLines[oldStart] == newLines[newStart])
            {
                edits.Add(new Edit(' ', oldStart, newStart, oldLines[oldStart]));
                oldStart++;
                newStart++;
            }

            var suffix = 0;
            while (oldStart < oldEnd - suffix && newStart < newEnd - suffix
                && oldLines[oldEnd - 1 - suffix] == newLines[newEnd - 1 - suffix])
                suffix++;

            AddMiddle(edits, oldLines, newLines, new DiffBlock(oldStart, oldEnd - suffix, newStart, newEnd - suffix));
            for (var i = suffix; i > 0; i--)
                edits.Add(new Edit(' ', oldEnd - i, newEnd - i, oldLines[oldEnd - i]));
        }

        private static void AddMiddle(
            List<Edit> edits,
            IReadOnlyList<DiffLine> oldLines,
            IReadOnlyList<DiffLine> newLines,
            DiffBlock middle)
        {
            var oldCount = middle.OldEnd - middle.OldStart;
            var newCount = middle.NewEnd - middle.NewStart;
            if ((long)(oldCount + 1) * (newCount + 1) > MaxTable)
            {
                for (var i = middle.OldStart; i < middle.OldEnd; i++)
                    edits.Add(new Edit('-', i, -1, oldLines[i]));

                for (var j = middle.NewStart; j < middle.NewEnd; j++)
                    edits.Add(new Edit('+', -1, j, newLines[j]));

                return;
            }

            // lengths[i, j] - длина общей подпоследовательности хвостов с i-й и j-й строки середины
            var lengths = new int[oldCount + 1, newCount + 1];
            for (var i = oldCount - 1; i >= 0; i--)
            {
                for (var j = newCount - 1; j >= 0; j--)
                {
                    lengths[i, j] = oldLines[middle.OldStart + i] == newLines[middle.NewStart + j]
                        ? lengths[i + 1, j + 1] + 1
                        : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
                }
            }

            int oldIndex = 0, newIndex = 0;
            while (oldIndex < oldCount || newIndex < newCount)
            {
                var oldLine = middle.OldStart + oldIndex;
                var newLine = middle.NewStart + newIndex;
                bool bothLeft = oldIndex < oldCount && newIndex < newCount;
                if (bothLeft && oldLines[oldLine] == newLines[newLine])
                {
                    edits.Add(new Edit(' ', oldLine, newLine, oldLines[oldLine]));
                    oldIndex++;
                    newIndex++;
                }
                else if (newIndex == newCount
                    || oldIndex < oldCount && lengths[oldIndex + 1, newIndex] >= lengths[oldIndex, newIndex + 1])
                {
                    edits.Add(new Edit('-', oldLine, -1, oldLines[oldLine]));
                    oldIndex++;
                }
                else
                {
                    edits.Add(new Edit('+', -1, newLine, newLines[newLine]));
                    newIndex++;
                }
            }
        }

        // Ханк от изменения index до разрыва больше 2 * Context общих строк, с контекстом; возвращает индекс после ханка
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
            {
                output.Append(edits[i].Kind).Append(edits[i].Line.Text).Append('\n');
                if (!edits[i].Line.HasEnding)
                    output.Append(NoEnding);
            }

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

        private record struct Edit(char Kind, int OldLine, int NewLine, DiffLine Line);
    }
}
