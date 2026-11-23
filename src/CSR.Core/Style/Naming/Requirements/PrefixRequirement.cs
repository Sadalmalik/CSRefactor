using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Requirements
{
    // Префикс имени. prefixStyle field (по умолчанию): prefix - обязательный префикс поля (m_Name, pascalAfterPrefix -
    // заглавная после префикса), "" - без префикса полей (m_, s_, k_, _ и knownPrefixes), "*" - не проверять.
    // prefixStyle word: префикс перед заглавной буквой или цифрой (IStorage, TValue), prefixAlone - имя может быть
    // самим префиксом (T). forbiddenPrefixes - имя не начинается с префикса перед заглавной буквой или цифрой
    // (OnChanged для delegate; Onboarding - не префикс)
    public class PrefixRequirement : INameRequirement
    {
        public const string Any = "*";

        public static readonly NameRequirementKind Kind = new(
            "prefix",
            ["prefix", "prefixStyle", "prefixAlone", "pascalAfterPrefix", "knownPrefixes", "forbiddenPrefixes"],
            Create);

        private readonly string? m_Prefix;
        private readonly bool m_IsWordStyle;
        private readonly bool m_IsAloneAllowed;
        private readonly bool m_IsPascalAfterPrefix;
        private readonly IReadOnlyList<string> m_Known;
        private readonly IReadOnlyList<string> m_Forbidden;

        public PrefixRequirement(
            string? prefix,
            bool isWordStyle = false,
            bool isAloneAllowed = false,
            bool isPascalAfterPrefix = false,
            IReadOnlyList<string>? known = null,
            IReadOnlyList<string>? forbidden = null)
        {
            m_Prefix = prefix;
            m_IsWordStyle = isWordStyle;
            m_IsAloneAllowed = isAloneAllowed;
            m_IsPascalAfterPrefix = isPascalAfterPrefix;
            m_Known = known ?? [];
            m_Forbidden = forbidden ?? [];
        }

        public string? Check(NameSymbol symbol)
        {
            var name = symbol.Name;
            var problem = m_Prefix == null || m_Prefix == Any
                ? null
                : m_IsWordStyle ? CheckWord(symbol, m_Prefix) : CheckField(symbol, m_Prefix);
            if (problem != null)
                return problem;

            var forbidden = m_Forbidden.FirstOrDefault(candidate => IsWordPrefix(name, candidate));
            return forbidden == null ? null : $"{symbol.Label} '{name}': без префикса '{forbidden}'";
        }

        private string? CheckWord(NameSymbol symbol, string prefix)
        {
            var name = symbol.Name;
            if (IsWordPrefix(name, prefix) || m_IsAloneAllowed && name == prefix)
                return null;

            return $"{symbol.Label} '{name}': префикс '{prefix}'";
        }

        private string? CheckField(NameSymbol symbol, string prefix)
        {
            var name = symbol.Name;
            if (prefix.Length == 0)
            {
                var found = FindFieldPrefix(name);
                return found == null ? null : $"'{name}': {symbol.Label} без префикса '{found}' - '{NameWords.ToPascal(name)}'";
            }

            var expected = prefix + NameWords.ToPascal(name);
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length == prefix.Length)
                return $"'{name}': {symbol.Label} с префиксом '{prefix}' - '{expected}'";

            if (m_IsPascalAfterPrefix && !char.IsUpper(name[prefix.Length]))
                return $"'{name}': после префикса - заглавная буква - '{expected}'";

            return null;
        }

        // Префикс полей в начале имени: из knownPrefixes или общий вид (m_, k_, _)
        private string? FindFieldPrefix(string name)
        {
            var known = m_Known.FirstOrDefault(prefix => prefix.Length > 0
                && name.Length > prefix.Length
                && name.StartsWith(prefix, StringComparison.Ordinal));
            if (known != null)
                return known;

            int length = name.Length - NameWords.StripFieldPrefix(name).Length;
            return length > 0 ? name.Substring(0, length) : null;
        }

        // Префикс перед заглавной буквой или цифрой: IStorage, OnChanged; не Item, не Onboarding
        private static bool IsWordPrefix(string name, string prefix)
        {
            return name.Length > prefix.Length
                && name.StartsWith(prefix, StringComparison.Ordinal)
                && !char.IsLower(name[prefix.Length]);
        }

        private static INameRequirement Create(Record record)
        {
            var style = record.GetChoice("prefixStyle", "field", "word") ?? "field";
            return new PrefixRequirement(
                record.GetString("prefix"),
                style == "word",
                record.GetBool("prefixAlone") ?? false,
                record.GetBool("pascalAfterPrefix") ?? false,
                record.GetStrings("knownPrefixes"),
                record.GetStrings("forbiddenPrefixes"));
        }
    }
}
