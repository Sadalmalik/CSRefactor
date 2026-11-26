using Microsoft.CodeAnalysis;

namespace CSR.Workspace.References
{
    public static class SourceLocationTools
    {
        private const int MaxTextLength = 200;

        public static CodeLocation Create(Location location, string root)
        {
            var span = location.GetLineSpan();
            var line = span.StartLinePosition.Line;
            var text = location.SourceTree?.GetText().Lines[line].ToString().Trim() ?? "";
            return new CodeLocation
            {
                File = GetRelativePath(root, span.Path),
                Line = line + 1,
                Column = span.StartLinePosition.Character + 1,
                Text = text.Length > MaxTextLength ? text.Substring(0, MaxTextLength) + "..." : text,
            };
        }

        // Путь относительно корня решения с разделителем '/'; вне корня и виртуальные пути - как есть
        public static string GetRelativePath(string root, string path)
        {
            if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path))
                return path.Replace('\\', '/');

            var relative = Path.GetRelativePath(root, path);
            bool outside = relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathFullyQualified(relative);
            return (outside ? path : relative).Replace('\\', '/');
        }
    }
}
