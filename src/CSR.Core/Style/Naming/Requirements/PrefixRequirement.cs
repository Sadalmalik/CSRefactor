using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Requirements
{
    // Префикс имени. forbiddenPrefixes - имя не начинается с префикса перед заглавной буквой или цифрой
    // (OnChanged для delegate; Onboarding - не префикс)
    public class PrefixRequirement : INameRequirement
    {
        public static readonly NameRequirementKind Kind = new("prefix", ["forbiddenPrefixes"], Create);

        private readonly IReadOnlyList<string> m_Forbidden;

        public PrefixRequirement(IReadOnlyList<string> forbidden)
        {
            m_Forbidden = forbidden;
        }

        public string? Check(NameSymbol symbol)
        {
            var prefix = m_Forbidden.FirstOrDefault(candidate => IsWordPrefix(symbol.Name, candidate));
            return prefix == null ? null : $"{symbol.Label} '{symbol.Name}': без префикса '{prefix}'";
        }

        // Префикс перед заглавной буквой или цифрой: IStorage, OnChanged; не Item, не Onboarding
        private static bool IsWordPrefix(string name, string prefix)
        {
            return name.Length > prefix.Length
                && name.StartsWith(prefix, StringComparison.Ordinal)
                && !char.IsLower(name[prefix.Length]);
        }

        private static INameRequirement Create(Record record)
            => new PrefixRequirement(record.GetStrings("forbiddenPrefixes"));
    }
}
