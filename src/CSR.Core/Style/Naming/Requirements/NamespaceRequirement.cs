using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Requirements
{
    // Корень пространства имен: пространство начинается с корня из roots (Company или Company.*); тип верхнего
    // уровня вне пространства имен - нарушение. Вложенный тип и тип внутри пространства не проверяются
    public class NamespaceRequirement : INameRequirement
    {
        public static readonly NameRequirementKind Kind = new("namespace", ["roots"], Create);

        private readonly IReadOnlyList<string> m_Roots;

        public NamespaceRequirement(IReadOnlyList<string> roots)
        {
            m_Roots = roots;
        }

        public string? Check(NameSymbol symbol)
        {
            if (symbol.Kind != "namespace")
                return symbol.Namespace == null && !symbol.IsNested
                    ? $"{symbol.Label} '{symbol.Name}' вне пространства имен"
                    : null;

            var name = symbol.Name;
            if (m_Roots.Any(root => name == root || name.StartsWith(root + ".", StringComparison.Ordinal)))
                return null;

            return $"{symbol.Label} '{name}' вне корня: {string.Join(", ", m_Roots)}";
        }

        private static INameRequirement Create(Record record)
            => new NamespaceRequirement(record.GetStrings("roots"));
    }
}
