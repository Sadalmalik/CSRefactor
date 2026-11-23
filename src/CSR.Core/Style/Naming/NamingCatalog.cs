using CSR.Core.Checks;
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
            MaskRequirement.Kind,
        ];

        public static readonly IReadOnlyList<SyntaxCheck> Aliases = [];
    }
}
