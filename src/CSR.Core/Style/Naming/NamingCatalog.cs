using CSR.Core.Checks;
using CSR.Core.Style.Naming.Legacy;
using CSR.Core.Style.Naming.Requirements;

namespace CSR.Core.Style.Naming
{
    // Реестр naming: виды требований (ключ каскада, поля записи) и старые проверки именования - псевдонимы,
    // которые переводят свои параметры в правила (до v0.4)
    public static class NamingCatalog
    {
        public static readonly IReadOnlyList<NameRequirementKind> Kinds =
        [
            PrefixRequirement.Kind,
            CaseRequirement.Kind,
            MaskRequirement.Kind,
            WordsRequirement.Kind,
            NumberRequirement.Kind,
            NamespaceRequirement.Kind,
            InNamespaceRequirement.Kind,
            DepthRequirement.Kind,
        ];

        public static readonly IReadOnlyList<SyntaxCheck> Aliases =
        [
            FieldPrefixLegacy.Check,
            EventNameLegacy.Check,
            TypeNameLegacy.Check,
            BoolNameLegacy.Check,
            EnumPluralLegacy.Check,
            CollectionNameLegacy.Check,
            NamespaceNameLegacy.Check,
        ];
    }
}
