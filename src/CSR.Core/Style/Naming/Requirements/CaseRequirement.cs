using CSR.Core.Checks;
using CSR.Core.Syntax;

namespace CSR.Core.Style.Naming.Requirements
{
    // Регистр имени: pascal - MaxSize (без '_', не UPPER_CASE; короткая аббревиатура UI, ID допустима), upper - MAX_SIZE
    public class CaseRequirement : INameRequirement
    {
        public static readonly NameRequirementKind Kind = new("case", ["case"], [], Create);

        private readonly bool m_IsUpper;

        private CaseRequirement(bool isUpper)
        {
            m_IsUpper = isUpper;
        }

        public string? Check(NameSymbol symbol)
        {
            var name = symbol.Name;
            if (!m_IsUpper && !IsPascal(name))
                return $"'{name}': {symbol.Label} в PascalCase - '{NameWordTools.ToPascal(name)}'";

            if (m_IsUpper && name.Any(char.IsLower))
                return $"'{name}': {symbol.Label} в UPPER_CASE - '{NameWordTools.ToUpperSnake(name)}'";

            return null;
        }

        private static bool IsPascal(string name)
        {
            if (!char.IsUpper(name[0]) || name.Contains("_"))
                return false;

            return name.Any(char.IsLower) || name.Count(char.IsLetter) <= 3;
        }

        private static INameRequirement Create(Record record)
            => new CaseRequirement(record.GetChoice("case", "pascal", "upper") == "upper");
    }
}
