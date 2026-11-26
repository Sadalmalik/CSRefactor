using CSR.Core.Checks;
using CSR.Core.Syntax;

namespace CSR.Core.Style.Naming.Requirements
{
    // Префикс имени. prefixStyle field (по умолчанию): prefix - обязательный префикс поля (m_Name, pascalAfterPrefix -
    // заглавная после префикса), "" - без префикса полей (m_, s_, k_, _ и knownPrefixes). prefixStyle word: префикс
    // перед заглавной буквой или цифрой (IStorage, TValue), prefixAlone - имя может быть самим префиксом (T).
    // forbiddenPrefixes - имя не начинается с префикса перед заглавной буквой или цифрой (OnChanged; Onboarding -
    // не префикс). Поле не своего стиля - ошибка конфига
    public class PrefixRequirement : INameRequirement
    {
        public static readonly NameRequirementKind Kind = new(
            "prefix",
            ["prefix", "forbiddenPrefixes"],
            ["prefixStyle", "prefixAlone", "pascalAfterPrefix", "knownPrefixes"],
            Create);

        private readonly string? m_Prefix;
        private readonly bool m_IsWordStyle;
        private readonly bool m_IsAloneAllowed;
        private readonly bool m_IsPascalAfterPrefix;
        private readonly IReadOnlyList<string> m_Known;
        private readonly IReadOnlyList<string> m_Forbidden;

        private PrefixRequirement(
            string? prefix,
            bool isWordStyle,
            bool isAloneAllowed,
            bool isPascalAfterPrefix,
            IReadOnlyList<string> known,
            IReadOnlyList<string> forbidden)
        {
            m_Prefix = prefix;
            m_IsWordStyle = isWordStyle;
            m_IsAloneAllowed = isAloneAllowed;
            m_IsPascalAfterPrefix = isPascalAfterPrefix;
            m_Known = known;
            m_Forbidden = forbidden;
        }

        public string? Check(NameSymbol symbol)
        {
            var name = symbol.Name;
            var problem = m_Prefix == null
                ? null
                : m_IsWordStyle
                    ? CheckWord(symbol, m_Prefix)
                    : CheckField(symbol, m_Prefix);
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
                return found == null
                    ? null
                    : $"'{name}': {symbol.Label} без префикса '{found}' - '{NameWordTools.ToPascal(name)}'";
            }

            var expected = prefix + NameWordTools.ToPascal(name);
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

            int length = name.Length - NameWordTools.StripFieldPrefix(name).Length;
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
            var prefix = record.GetString("prefix");
            bool isWordStyle = (record.GetChoice("prefixStyle", "field", "word") ?? "field") == "word";
            if (prefix == null && record.Has("prefixStyle"))
                throw new FormatException("prefixStyle: только вместе с prefix");

            if (isWordStyle)
                RequireAbsent(record, "pascalAfterPrefix", "knownPrefixes");
            else
                RequireAbsent(record, "prefixAlone");

            if (isWordStyle && prefix?.Length == 0)
                throw new FormatException("prefix: в стиле word префикс не пустой");

            if (prefix?.Length > 0 && record.Has("knownPrefixes"))
                throw new FormatException("knownPrefixes: только с prefix \"\"");

            return new PrefixRequirement(
                prefix,
                isWordStyle,
                record.GetBool("prefixAlone") ?? false,
                record.GetBool("pascalAfterPrefix") ?? false,
                record.GetStrings("knownPrefixes"),
                record.GetStrings("forbiddenPrefixes"));
        }

        private static void RequireAbsent(Record record, params string[] fields)
        {
            var field = fields.FirstOrDefault(record.Has);
            if (field != null)
                throw new FormatException($"{field}: не для этого prefixStyle");
        }
    }
}
