using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Requirements
{
    // Корень пространства имен: имя начинается с корня из roots (Company или Company.*)
    public class NamespaceRequirement : INameRequirement
    {
        public static readonly NameRequirementKind Kind = new("namespace", ["roots"], [], Create);

        private readonly IReadOnlyList<string> m_Roots;

        private NamespaceRequirement(IReadOnlyList<string> roots)
        {
            m_Roots = roots;
        }

        public string? Check(NameSymbol symbol)
        {
            var name = symbol.Name;
            if (m_Roots.Any(root => name == root || name.StartsWith(root + ".", StringComparison.Ordinal)))
                return null;

            return $"{symbol.Label} '{name}' вне корня: {string.Join(", ", m_Roots)}";
        }

        private static INameRequirement Create(Record record)
        {
            var roots = record.GetStrings("roots");
            if (roots.Count == 0)
                throw new FormatException("roots: нужен хотя бы один корень");

            return new NamespaceRequirement(roots);
        }
    }
}
