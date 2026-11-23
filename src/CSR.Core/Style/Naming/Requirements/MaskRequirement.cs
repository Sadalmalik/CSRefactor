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
        private readonly IReadOnlyList<string> m_Forbidden;

        private MaskRequirement(IReadOnlyList<string> allowed, IReadOnlyList<string> forbidden)
        {
            m_Allowed = allowed;
            m_Forbidden = forbidden;
        }

        public string? Check(NameSymbol symbol)
        {
            var mask = m_Forbidden.FirstOrDefault(candidate => NameMask.Get([candidate]).IsMatch(symbol.Name));
            if (mask != null)
                return $"{symbol.Label} '{symbol.Name}': запрещенная форма '{mask}'";

            if (m_Allowed.Count > 0 && !NameMask.Get(m_Allowed).IsMatch(symbol.Name))
                return $"{symbol.Label} '{symbol.Name}': допустимо {string.Join(", ", m_Allowed)}";

            return null;
        }

        private static INameRequirement Create(Record record)
            => new MaskRequirement(record.GetStrings("allowed"), record.GetStrings("forbidden"));
    }
}
