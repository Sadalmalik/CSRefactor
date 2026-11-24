using CSR.Reports.Diff;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Refactor
{
    // Unified diff по правкам текста (TextChange): блоки - по правкам, пути a/<путь> и b/<путь>, как у git
    public static class TextDiff
    {
        public static string Build(
            string path,
            SourceText before,
            SourceText after,
            IReadOnlyList<TextChange> changes)
        {
            if (changes.Count == 0)
                return "";

            var oldLines = GetLines(before);
            var newLines = GetLines(after);
            var blocks = GetBlocks(before, changes)
                .Select(block => new DiffBlock(
                    Math.Min(block.OldStart, oldLines.Count),
                    Math.Min(block.OldEnd, oldLines.Count),
                    Math.Min(block.NewStart, newLines.Count),
                    Math.Min(block.NewEnd, newLines.Count)))
                .ToList();
            return DiffWriter.Write($"a/{path}", $"b/{path}", oldLines, newLines, blocks);
        }

        // Новый файл: все строки добавлены
        public static string BuildAdded(string path, SourceText text)
        {
            var lines = GetLines(text);
            return DiffWriter.Write("/dev/null", $"b/{path}", [], lines, [new DiffBlock(0, 0, 0, lines.Count)]);
        }

        // Удаленный файл: все строки удалены
        public static string BuildRemoved(string path, SourceText text)
        {
            var lines = GetLines(text);
            return DiffWriter.Write($"a/{path}", "/dev/null", lines, [], [new DiffBlock(0, lines.Count, 0, 0)]);
        }

        // Перенос файла без правок текста - заголовок переноса git
        public static string BuildRenamed(string oldPath, string path)
        {
            return $"diff --git a/{oldPath} b/{path}\nsimilarity index 100%\n"
                + $"rename from {oldPath}\nrename to {path}\n";
        }

        // Строки текста; пустая строка после завершающего перевода строки не считается
        private static List<DiffLine> GetLines(SourceText text)
        {
            var lines = text.Lines
                .Select(line => new DiffLine(line.ToString(), line.EndIncludingLineBreak > line.End))
                .ToList();
            if (lines.Count > 0 && lines[^1] is { Text.Length: 0, HasEnding: false })
                lines.RemoveAt(lines.Count - 1);

            return lines;
        }

        // Блоки по строкам SourceText (с последней пустой строкой); правки в одной или соседних строках - один блок
        private static List<DiffBlock> GetBlocks(SourceText before, IReadOnlyList<TextChange> changes)
        {
            var blocks = new List<DiffBlock>();
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
                    blocks.Add(new DiffBlock(oldStart, oldEnd, newStart, oldEnd + shift + lineDelta));
                }

                shift += lineDelta;
            }

            return blocks;
        }

        private static int CountLines(string text)
            => text.Count(character => character == '\n');
    }
}
