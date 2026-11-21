using System.Text.RegularExpressions;

namespace CSR.Workspace.Symbols
{
    // Символ из командной строки: "Namespace.Type.Member", "Namespace.Type.Method(int, string)"
    // или "файл:строка[:колонка]" (строка и колонка с 1)
    public class SymbolQuery
    {
        private const string ExpectedFormat = "ожидается Namespace.Type.Member или файл.cs:строка[:колонка]";

        private static readonly Regex PositionPattern = new(@"^(?<file>.+\.cs):(?<line>\d+)(?::(?<column>\d+))?$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex NamePattern = new(
            @"^(?<name>[\p{L}_@][\p{L}\p{Nd}_.]*)(?:\((?<parameters>[^)]*)\))?$",
            RegexOptions.CultureInvariant);

        private SymbolQuery(string text)
        {
            Text = text;
        }

        public string Text { get; }

        public string? File { get; private init; }

        public int Line { get; private init; }

        public int? Column { get; private init; }

        public string? Name { get; private init; }

        // null - перегрузки не уточнены; пустой список - метод без параметров
        public IReadOnlyList<string>? Parameters { get; private init; }

        public bool IsPosition
            => File != null;

        public static SymbolQuery Parse(string text)
        {
            var trimmed = text.Trim();
            var position = PositionPattern.Match(trimmed);
            if (position.Success)
            {
                return new SymbolQuery(trimmed)
                {
                    File = position.Groups["file"].Value,
                    Line = int.Parse(position.Groups["line"].Value),
                    Column = position.Groups["column"].Success ? int.Parse(position.Groups["column"].Value) : null,
                };
            }

            if (trimmed.StartsWith("global::", StringComparison.Ordinal))
                trimmed = trimmed.Substring("global::".Length);

            var name = NamePattern.Match(trimmed.Replace(" ", ""));
            if (!name.Success)
                throw new SymbolException($"символ '{text}': {ExpectedFormat}");

            var parameters = name.Groups["parameters"];
            return new SymbolQuery(trimmed)
            {
                Name = name.Groups["name"].Value.Replace("@", ""),
                Parameters = parameters.Success ? SplitParameters(parameters.Value) : null,
            };
        }

        public string LastSegment
            => Name!.Substring(Name.LastIndexOf('.') + 1);

        private static List<string> SplitParameters(string text)
            => text.Length == 0 ? [] : text.Split(',').ToList();
    }

    public class SymbolException : Exception
    {
        public SymbolException(string message)
            : base(message)
        {
        }
    }
}
