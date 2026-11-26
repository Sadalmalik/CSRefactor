using Microsoft.CodeAnalysis;

namespace CSR.Core.Checks
{
    // Текст сообщений находок: перечисление имен, узел в одну строку
    public static class FindingTextTools
    {
        // Перечисление имен для сообщения: не больше limit, остальное - числом
        public static string JoinNames(IReadOnlyList<string> names, int limit = 8)
        {
            if (names.Count <= limit)
                return string.Join(", ", names);

            return string.Join(", ", names.Take(limit)) + $" и еще {names.Count - limit}";
        }

        // Текст узла в одну строку, не длиннее limit
        public static string Shorten(SyntaxNode node, int limit = 80)
        {
            var text = string.Join(" ", node.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length <= limit ? text : text.Substring(0, limit - 3) + "...";
        }
    }
}
