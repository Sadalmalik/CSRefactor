using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Rename
{
    // Unified diff по правкам текста (TextChange): строки вне правок совпадают по построению,
    // поэтому полный построчный дифф не нужен. Пути - a/<путь> и b/<путь>, как у git: git apply его примет
    public static class TextDiff
    {
        private const int Context = 3;

        public static string Build(string path, SourceText before, SourceText after,
            IReadOnlyList<TextChange> changes)
        {
            if (changes.Count == 0)
                return "";

            var blocks = GetBlocks(before, changes);
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

        private static void AppendHunk(StringBuilder output, SourceText before, SourceText after, List<Block> blocks,
            int first, int last)
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

                for (var line = block.OldStart; line < block.OldEnd; line++)
                    output.Append('-').Append(before.Lines[line].ToString()).Append('\n');

                for (var line = block.NewStart; line < block.NewEnd; line++)
                    output.Append('+').Append(after.Lines[line].ToString()).Append('\n');

                oldLine = block.OldEnd;
            }

            for (; oldLine < oldEnd; oldLine++)
                output.Append(' ').Append(before.Lines[oldLine].ToString()).Append('\n');
        }

        private static string FormatRange(int start, int count)
            => count == 0 ? $"{start},0" : $"{start + 1},{count}";

        private static int CountLines(string text)
            => text.Count(character => character == '\n');

        private record struct Block(int OldStart, int OldEnd, int NewStart, int NewEnd);
    }
}
