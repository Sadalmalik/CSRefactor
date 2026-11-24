using CSR.Core.Checks;
using CSR.Core.Syntax;

namespace CSR.Core.Style.Naming.Requirements
{
    // Запрещенные слова имени: слово из forbiddenWords или слово с приставкой из negationPrefixes (Disabled),
    // кроме ignoreWords; без учета регистра. Имя делится на слова без префикса поля, пространство - по частям.
    // negation - слова означают отрицание: в сообщении "имя по состоянию"
    public class WordsRequirement : INameRequirement
    {
        public static readonly NameRequirementKind Kind = new(
            "words",
            ["forbiddenWords", "negationPrefixes"],
            ["ignoreWords", "negation"],
            Create);

        private readonly IReadOnlyList<string> m_Forbidden;
        private readonly IReadOnlyList<string> m_Prefixes;
        private readonly IReadOnlyList<string> m_Ignored;
        private readonly bool m_IsNegation;

        private WordsRequirement(
            IReadOnlyList<string> forbidden,
            IReadOnlyList<string> prefixes,
            IReadOnlyList<string> ignored,
            bool isNegation)
        {
            m_Forbidden = forbidden;
            m_Prefixes = prefixes;
            m_Ignored = ignored;
            m_IsNegation = isNegation;
        }

        public string? Check(NameSymbol symbol)
        {
            var words = symbol.Name.Split('.')
                .SelectMany(part => NameWords.GetWords(part))
                .ToList();
            var word = NameWords.FindWord(words, m_Forbidden) ?? FindPrefixed(words);
            if (word == null)
                return null;

            return m_IsNegation
                ? $"{symbol.Label} '{symbol.Name}': отрицание '{word}' - имя по состоянию"
                : $"{symbol.Label} '{symbol.Name}': слово '{word}'";
        }

        // Слово, которое начинается с приставки и длиннее нее (Disabled, Unlocked), кроме ignoreWords
        private string? FindPrefixed(IReadOnlyList<string> words)
        {
            foreach (var word in words)
            {
                bool isPrefixed = m_Prefixes.Any(prefix => word.Length > prefix.Length
                    && word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (isPrefixed && !m_Ignored.Contains(word, StringComparer.OrdinalIgnoreCase))
                    return word;
            }

            return null;
        }

        private static INameRequirement Create(Record record)
        {
            return new WordsRequirement(
                record.GetStrings("forbiddenWords"),
                record.GetStrings("negationPrefixes"),
                record.GetStrings("ignoreWords"),
                record.GetBool("negation") ?? false);
        }
    }
}
