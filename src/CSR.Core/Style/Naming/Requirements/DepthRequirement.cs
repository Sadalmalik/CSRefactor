using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Requirements
{
    // Глубина пространства имен: не больше maxParts частей
    public class DepthRequirement : INameRequirement
    {
        public static readonly NameRequirementKind Kind = new("depth", ["maxParts"], [], Create);

        private readonly int m_MaxParts;

        private DepthRequirement(int maxParts)
        {
            m_MaxParts = maxParts;
        }

        public string? Check(NameSymbol symbol)
        {
            int parts = symbol.Name.Split('.').Length;
            return parts > m_MaxParts ? $"{symbol.Label} '{symbol.Name}': частей {parts}, предел {m_MaxParts}" : null;
        }

        private static INameRequirement Create(Record record)
        {
            var maxParts = record.GetInt("maxParts");
            if (maxParts is not > 0)
                throw new FormatException("maxParts: число больше 0");

            return new DepthRequirement(maxParts.Value);
        }
    }
}
