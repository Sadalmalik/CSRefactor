using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Rename
{
    // Unified diff по правкам текста (TextChange): строки вне правок совпадают по построению,
    // поэтому полный построчный дифф не нужен. Пути - a/<путь> и b/<путь>, как у git: git apply его примет
    public static class TextDiff
    {
        private const int Context = 3;

        public static string Build(
            string path,
            SourceText before,
            SourceText after,
            IReadOnlyList<TextChange> changes)
        {
            if (changes.Count == 0)
                return "";

            var blocks = GetBlocks(before, changes)
                .Select(block => Trim(before, after, block))
                .Where(block => block.OldStart < block.OldEnd || block.NewStart < block.NewEnd)
                .ToList();
            if (blocks.Count == 0)
                return "";

            var output = new StringBuilder();
            output.Append($"--- a/{path}\n+++ b/{path}\n");
            var index = 0;
            while (index < blocks.Count)
            {
                var last = index;
                while (last + 1 < blocks.Count && blocks[last + 1].OldStart - blocks[last].OldEnd <= 2 * Context)
                    last++;

                AppendHunk(output, before, after, blocks, index, last);
                index = last + 1;
            }

            return output.ToString();
        }

        // Новый файл: все строки добавлены
        public static string BuildAdded(string path, SourceText text)
        {
            var output = new StringBuilder();
            var lines = GetLines(text);
            output.Append($"--- /dev/null\n+++ b/{path}\n@@ -0,0 +{FormatRange(0, lines.Count)} @@\n");
            foreach (var line in lines)
                output.Append('+').Append(line).Append('\n');

            return output.ToString();
        }

        // Удаленный файл: все строки удалены
        public static string BuildRemoved(string path, SourceText text)
        {
            var output = new StringBuilder();
            var lines = GetLines(text);
            output.Append($"--- a/{path}\n+++ /dev/null\n@@ -{FormatRange(0, lines.Count)} +0,0 @@\n");
            foreach (var line in lines)
                output.Append('-').Append(line).Append('\n');

            return output.ToString();
        }

        // Перенос файла без правок текста - заголовок переноса git
        public static string BuildRenamed(string oldPath, string path)
        {
            return $"diff --git a/{oldPath} b/{path}\nsimilarity index 100%\n"
                + $"rename from {oldPath}\nrename to {path}\n";
        }

        // Строки текста; последняя пустая строка после завершающего перевода строки не считается
        private static List<string> GetLines(SourceText text)
        {
            var lines = text.Lines.Select(line => line.ToString()).ToList();
            if (lines.Count > 0 && lines[^1].Length == 0)
                lines.RemoveAt(lines.Count - 1);

            return lines;
        }

        // Блок - строки [OldStart, OldEnd) старого текста заменены строками [NewStart, NewEnd) нового;
        // правки в одной или соседних строках - один блок
        private static List<Block> GetBlocks(SourceText before, IReadOnlyList<TextChange> changes)
        {
            var blocks = new List<Block>();
            var shift = 0;
            foreach (var change in changes)
            {
                var oldStart = before.Lines.GetLineFromPosition(change.Span.Start).LineNumber;
                var oldEnd = before.Lines.GetLineFromPosition(change.Span.End).LineNumber + 1;
                var lineDelta = CountLines(change.NewText!) - CountLines(before.ToString(change.Span));
                var newStart = oldStart + shift;
                if (blocks.Count > 0 && oldStart <= blocks[^1].OldEnd)
                {
                    var previous = blocks[^1];
                    blocks[^1] = previous with
                    {
                        OldEnd = Math.Max(previous.OldEnd, oldEnd),
                        NewEnd = Math.Max(previous.OldEnd, oldEnd) + shift + lineDelta,
                    };
                }
                else
                {
                    blocks.Add(new Block(oldStart, oldEnd, newStart, oldEnd + shift + lineDelta));
                }

                shift += lineDelta;
            }

            return blocks;
        }

        // Правка не по границам строк (дифф текста Roslyn) захватывает одинаковые строки на краях блока
        private static Block Trim(SourceText before, SourceText after, Block block)
        {
            while (block.OldStart < block.OldEnd && block.NewStart < block.NewEnd
                && IsSameLine(before, block.OldStart, after, block.NewStart))
                block = block with { OldStart = block.OldStart + 1, NewStart = block.NewStart + 1 };

            while (block.OldStart < block.OldEnd && block.NewStart < block.NewEnd
                && IsSameLine(before, block.OldEnd - 1, after, block.NewEnd - 1))
                block = block with { OldEnd = block.OldEnd - 1, NewEnd = block.NewEnd - 1 };

            return block;
        }

        private static bool IsSameLine(SourceText before, int oldLine, SourceText after, int newLine)
            => before.Lines[oldLine].ToString() == after.Lines[newLine].ToString();

        private static void AppendHunk(
            StringBuilder output,
            SourceText before,
            SourceText after,
            List<Block> blocks,
            int first,
            int last)
        {
            var oldStart = Math.Max(0, blocks[first].OldStart - Context);
            var oldEnd = Math.Min(before.Lines.Count, blocks[last].OldEnd + Context);
            var newStart = blocks[first].NewStart - (blocks[first].OldStart - oldStart);
            var newEnd = blocks[last].NewEnd + (oldEnd - blocks[last].OldEnd);
            var oldRange = FormatRange(oldStart, oldEnd - oldStart);
            var newRange = FormatRange(newStart, newEnd - newStart);
            output.Append($"@@ -{oldRange} +{newRange} @@\n");

            var oldLine = oldStart;
            for (var i = first; i <= last; i++)
            {
                var block = blocks[i];
                for (; oldLine < block.OldStart; oldLine++)
                    output.Append(' ').Append(before.Lines[oldLine].ToString()).Append('\n');

                AppendBlock(output, before, after, block);

                oldLine = block.OldEnd;
            }

            for (; oldLine < oldEnd; oldLine++)
                output.Append(' ').Append(before.Lines[oldLine].ToString()).Append('\n');
        }

        // Строки блока: общие строки старого и нового (наибольшая общая подпоследовательность) - без изменений,
        // остальные - удалены и добавлены
        private static void AppendBlock(StringBuilder output, SourceText before, SourceText after, Block block)
        {
            var removed = Enumerable.Range(block.OldStart, block.OldEnd - block.OldStart)
                .Select(line => before.Lines[line].ToString())
                .ToList();
            var added = Enumerable.Range(block.NewStart, block.NewEnd - block.NewStart)
                .Select(line => after.Lines[line].ToString())
                .ToList();
            var common = new int[removed.Count + 1, added.Count + 1];
            for (var i = removed.Count - 1; i >= 0; i--)
            {
                for (var j = added.Count - 1; j >= 0; j--)
                {
                    common[i, j] = removed[i] == added[j]
                        ? common[i + 1, j + 1] + 1
                        : Math.Max(common[i + 1, j], common[i, j + 1]);
                }
            }

            var oldIndex = 0;
            var newIndex = 0;
            while (oldIndex < removed.Count || newIndex < added.Count)
            {
                if (oldIndex < removed.Count && newIndex < added.Count && removed[oldIndex] == added[newIndex])
                {
                    output.Append(' ').Append(removed[oldIndex++]).Append('\n');
                    newIndex++;
                }
                else if (newIndex == added.Count
                    || oldIndex < removed.Count && common[oldIndex + 1, newIndex] >= common[oldIndex, newIndex + 1])
                {
                    output.Append('-').Append(removed[oldIndex++]).Append('\n');
                }
                else
                {
                    output.Append('+').Append(added[newIndex++]).Append('\n');
                }
            }
        }

        private static string FormatRange(int start, int count)
            => count == 0 ? $"{start},0" : $"{start + 1},{count}";

        private static int CountLines(string text)
            => text.Count(character => character == '\n');

        private record struct Block(int OldStart, int OldEnd, int NewStart, int NewEnd);
    }
}
