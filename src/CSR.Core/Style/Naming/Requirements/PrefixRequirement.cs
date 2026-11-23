using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Requirements
{
    // Префикс имени. prefix с prefixStyle word - префикс перед заглавной буквой или цифрой (IStorage, TValue),
    // prefixAlone - имя может быть самим префиксом (T); forbiddenPrefixes - имя не начинается с префикса перед
    // заглавной буквой или цифрой (OnChanged для delegate; Onboarding - не префикс)
    public class PrefixRequirement : INameRequirement
    {
        public static readonly NameRequirementKind Kind = new(
            "prefix",
            ["prefix", "prefixStyle", "prefixAlone", "forbiddenPrefixes"],
            Create);

        private readonly string? m_Prefix;
        private readonly bool m_IsAloneAllowed;
        private readonly IReadOnlyList<string> m_Forbidden;

        public PrefixRequirement(string? prefix, bool isAloneAllowed, IReadOnlyList<string> forbidden)
        {
            m_Prefix = prefix;
            m_IsAloneAllowed = isAloneAllowed;
            m_Forbidden = forbidden;
        }

        public string? Check(NameSymbol symbol)
        {
            var name = symbol.Name;
            if (m_Prefix != null && !IsWordPrefix(name, m_Prefix) && !(m_IsAloneAllowed && name == m_Prefix))
                return $"{symbol.Label} '{name}': префикс '{m_Prefix}'";

            var forbidden = m_Forbidden.FirstOrDefault(candidate => IsWordPrefix(name, candidate));
            return forbidden == null ? null : $"{symbol.Label} '{name}': без префикса '{forbidden}'";
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
            record.GetChoice("prefixStyle", "word");
            var prefix = record.GetString("prefix");
            if (prefix != null && record.GetString("prefixStyle") == null)
                throw new FormatException("prefix: нужен prefixStyle");

            return new PrefixRequirement(prefix, record.GetBool("prefixAlone") ?? false, record.GetStrings("forbiddenPrefixes"));
        }
    }
}
