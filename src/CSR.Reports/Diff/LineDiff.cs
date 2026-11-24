namespace CSR.Reports.Diff
{
    // Unified diff двух текстов построчно (пути a/<путь>, b/<путь>): весь текст - один блок
    public static class LineDiff
    {
        public static string Build(string path, string before, string after)
        {
            if (before == after)
                return "";

            var oldLines = Split(before);
            var newLines = Split(after);
            return DiffWriter.Write(
                $"a/{path}",
                $"b/{path}",
                oldLines,
                newLines,
                [new DiffBlock(0, oldLines.Count, 0, newLines.Count)]);
        }

        // Строки без переводов (\r\n или \n); перевод в конце файла не дает пустой строки
        private static List<DiffLine> Split(string text)
        {
            if (text.Length == 0)
                return [];

            var lines = text.Replace("\r\n", "\n").Split('\n');
            bool hasEnding = text.EndsWith("\n", StringComparison.Ordinal);
            var count = hasEnding ? lines.Length - 1 : lines.Length;
            return lines
                .Take(count)
                .Select((line, index) => new DiffLine(line, hasEnding || index < count - 1))
                .ToList();
        }
    }
}
