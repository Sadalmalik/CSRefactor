using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Requirements
{
    // Объявление верхнего уровня внутри пространства имен: тип вне типа и вне namespace - нарушение
    public class InNamespaceRequirement : INameRequirement
    {
        public static readonly NameRequirementKind Kind = new("inNamespace", ["inNamespace"], [], Create);

        public string? Check(NameSymbol symbol)
            => symbol.Namespace == null && !symbol.IsNested ? $"{symbol.Label} '{symbol.Name}' вне пространства имен" : null;

        private static INameRequirement Create(Record record)
        {
            if (record.GetBool("inNamespace") != true)
                throw new FormatException("inNamespace: только true");

            return new InNamespaceRequirement();
        }
    }
}
