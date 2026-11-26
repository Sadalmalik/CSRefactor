using Microsoft.CodeAnalysis;

namespace CSR.Core.Similar
{
    // Единицы сравнения одного файла по видам из options.Kinds
    public static class SimilarUnitTool
    {
        public static List<SimilarUnit> Collect(SyntaxTree tree, SimilarOptions options)
        {
            var kinds = options.Kinds;
            var units = new List<SimilarUnit>();
            if (kinds.Contains(UnitKind.Member) || kinds.Contains(UnitKind.Fragment))
                units.AddRange(MemberUnitTool.Collect(tree, options));

            if (kinds.Contains(UnitKind.ParameterSet) || kinds.Contains(UnitKind.FieldSet))
                units.AddRange(SetUnitTool.Collect(tree, options));

            if (kinds.Contains(UnitKind.Type))
                units.AddRange(TypeUnitTool.Collect(tree, options));

            return units;
        }
    }
}
