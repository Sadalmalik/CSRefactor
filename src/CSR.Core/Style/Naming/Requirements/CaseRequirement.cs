using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Requirements
{
    // Регистр имени: pascal - MaxSize (без '_', не UPPER_CASE; короткая аббревиатура UI, ID допустима), upper - MAX_SIZE,
    // any - не проверять (занимает вид в каскаде)
    public class CaseRequirement : INameRequirement
    {
        public static readonly NameRequirementKind Kind = new("case", ["case"], Create);

        private readonly string m_Case;

        public CaseRequirement(string nameCase)
        {
            m_Case = nameCase;
        }

        public string? Check(NameSymbol symbol)
        {
            var name = symbol.Name;
            if (m_Case == "pascal" && !IsPascal(name))
                return $"'{name}': {symbol.Label} в PascalCase - '{NameWords.ToPascal(name)}'";

            if (m_Case == "upper" && name.Any(char.IsLower))
                return $"'{name}': {symbol.Label} в UPPER_CASE - '{NameWords.ToUpperSnake(name)}'";

            return null;
        }

        private static bool IsPascal(string name)
        {
            if (!char.IsUpper(name[0]) || name.Contains("_"))
                return false;

            return name.Any(char.IsLower) || name.Count(char.IsLetter) <= 3;
        }

        private static INameRequirement Create(Record record)
            => new CaseRequirement(record.GetChoice("case", "pascal", "upper", "any")!);
    }
}
