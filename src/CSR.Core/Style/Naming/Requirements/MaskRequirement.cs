using CSR.Core.Arch;
using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Requirements
{
    // Маски имени: allowed - имя подходит хотя бы под одну, forbidden - ни под одну.
    // Сначала проверяется forbidden: запрещенная форма точнее подсказывает, что не так
    public class MaskRequirement : INameRequirement
    {
        public static readonly NameRequirementKind Kind = new("masks", ["allowed", "forbidden"], [], Create);

        private readonly IReadOnlyList<string> m_Allowed;
        private readonly NameMask m_AllowedMask;
        private readonly IReadOnlyList<KeyValuePair<string, NameMask>> m_Forbidden;

        private MaskRequirement(IReadOnlyList<string> allowed, IReadOnlyList<string> forbidden)
        {
            m_Allowed = allowed;
            m_AllowedMask = NameMask.Get(allowed);
            m_Forbidden = forbidden.Select(mask => new KeyValuePair<string, NameMask>(mask, NameMask.Get([mask]))).ToList();
        }

        public string? Check(NameSymbol symbol)
        {
            foreach (var forbidden in m_Forbidden)
            {
                if (forbidden.Value.IsMatch(symbol.Name))
                    return $"{symbol.Label} '{symbol.Name}': запрещенная форма '{forbidden.Key}'";
            }

            if (!m_AllowedMask.IsEmpty && !m_AllowedMask.IsMatch(symbol.Name))
                return $"{symbol.Label} '{symbol.Name}': допустимо {string.Join(", ", m_Allowed)}";

            return null;
        }

        private static INameRequirement Create(Record record)
            => new MaskRequirement(record.GetStrings("allowed"), record.GetStrings("forbidden"));
    }
}
