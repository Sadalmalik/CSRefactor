using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // Интерфейс с одной реализацией: среди неабстрактных классов и структур выбранных проектов интерфейс
    // (с наследованием) реализует один тип. Точка расширения или граница подсистемы - контракт-комментарий
    // над интерфейсом (маски contractMasks) или атрибут из ignoreAttributes - не находка
    public class SingleImplementationCheck : SolutionCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings(
                "contractMasks",
                "маски текста комментария-контракта над интерфейсом",
                "Реализу*",
                "Implement*"),
            CheckParameter.Strings("ignoreAttributes", "интерфейсы с этими атрибутами не ищутся"),
            CheckParameter.Bool("ignoreMarkers", true, "не искать интерфейсы без членов"),
        ];

        public override string Name
            => "single-implementation";

        public override string Description
            => "интерфейс с одной реализацией";

        public override string Question
            => "Интерфейс - точка расширения или граница подсистемы? "
                + "Если нет - убрать, оставив класс; если да - контракт-комментарий";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        public override void AnalyzeSolution(SolutionCheckContext context)
        {
            var implementations = new Dictionary<string, List<INamedTypeSymbol>>();
            foreach (var declared in context.Index.Types)
            {
                var type = declared.Symbol;
                if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) || type.IsAbstract)
                    continue;

                foreach (var contract in type.AllInterfaces)
                {
                    var key = SolutionIndex.GetKey(contract);
                    if (!implementations.TryGetValue(key, out List<INamedTypeSymbol>? list))
                    {
                        list = new List<INamedTypeSymbol>();
                        implementations[key] = list;
                    }

                    list.Add(type);
                }
            }

            foreach (var declared in context.Index.Types)
            {
                var contract = declared.Symbol;
                if (contract.TypeKind != TypeKind.Interface || IsExempt(context, contract))
                    continue;

                if (!implementations.TryGetValue(SolutionIndex.GetKey(contract), out List<INamedTypeSymbol>? list)
                    || list.Count != 1)
                    continue;

                var implementation = list[0];
                var file = implementation.Locations
                    .FirstOrDefault(location => location.IsInSource)?.SourceTree?.FilePath;
                var where = file == null ? "" : $" ({System.IO.Path.GetFileName(context.GetDisplayPath(file))})";
                var message = $"интерфейс '{contract.Name}' - одна реализация: '{implementation.Name}'{where}";
                context.Report(contract.Locations.First(location => location.IsInSource), message);
            }
        }

        private static bool IsExempt(SolutionCheckContext context, INamedTypeSymbol contract)
        {
            if (context.Settings.GetBool("ignoreMarkers") && contract.GetMembers().IsEmpty)
                return true;

            var attributes = NameMask.Get(context.Settings.GetStrings("ignoreAttributes"));
            if (contract.GetAttributes().Any(attribute => attribute.AttributeClass != null
                && attributes.IsMatch(TypeNames.TrimAttributeSuffix(attribute.AttributeClass.Name))))
                return true;

            var contracts = NameMask.Get(context.Settings.GetStrings("contractMasks"), true);
            return contract.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(context.Cancellation))
                .OfType<InterfaceDeclarationSyntax>()
                .Any(declaration => HasContract(declaration, contracts));
        }

        // Комментарий над объявлением (до атрибутов и модификаторов): текст после // по маске
        private static bool HasContract(InterfaceDeclarationSyntax declaration, NameMask contracts)
        {
            return declaration.GetLeadingTrivia()
                .Select(trivia => trivia.ToString().Trim())
                .Where(text => text.StartsWith("//", StringComparison.Ordinal))
                .Select(text => text.TrimStart('/').Trim())
                .Any(contracts.IsMatch);
        }
    }
}
