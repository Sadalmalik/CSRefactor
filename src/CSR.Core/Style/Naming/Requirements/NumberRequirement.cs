using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Requirements
{
    // Число имени по последнему слову (без префикса поля): множественное - слово из pluralWords или на 's', кроме
    // окончаний singularEndings (по умолчанию ss, us, is: Status, Class, Axis); number plural или singular
    public class NumberRequirement : INameRequirement
    {
        public static readonly NameRequirementKind Kind = new("number", ["number"], ["pluralWords", "singularEndings"], Create);

        private static readonly IReadOnlyList<string> DefaultSingularEndings = ["ss", "us", "is"];

        private readonly bool m_IsPlural;
        private readonly IReadOnlyList<string> m_PluralWords;
        private readonly IReadOnlyList<string> m_SingularEndings;

        private NumberRequirement(bool isPlural, IReadOnlyList<string> pluralWords, IReadOnlyList<string> singularEndings)
        {
            m_IsPlural = isPlural;
            m_PluralWords = pluralWords;
            m_SingularEndings = singularEndings;
        }

        public string? Check(NameSymbol symbol)
        {
            var word = NameWords.GetLastWord(symbol.Name);
            bool isPlural = NameWords.IsPlural(word, m_PluralWords, m_SingularEndings);
            if (isPlural == m_IsPlural)
                return null;

            var number = m_IsPlural ? "во множественном числе" : "в единственном числе";
            return $"{symbol.Label} '{symbol.Name}': имя {number}";
        }

        private static INameRequirement Create(Record record)
        {
            var number = record.GetChoice("number", "plural", "singular");
            var endings = record.Has("singularEndings") ? record.GetStrings("singularEndings") : DefaultSingularEndings;
            return new NumberRequirement(number == "plural", record.GetStrings("pluralWords"), endings);
        }
    }
}
