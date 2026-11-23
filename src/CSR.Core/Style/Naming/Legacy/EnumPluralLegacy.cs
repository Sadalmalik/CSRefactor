using CSR.Core.Checks;
using CSR.Core.Style.Naming.Requirements;

namespace CSR.Core.Style.Naming.Legacy
{
    // enum-plural до v0.4: число в имени enum по [Flags] -> два правила naming (с [Flags] - множественное, иначе
    // единственное)
    public static class EnumPluralLegacy
    {
        public static readonly NamingAlias Check = new(
            "enum-plural",
            "число в имени enum по [Flags] (устарела: naming)",
            [
                CheckParameter.Strings("pluralWords", "слова во множественном числе без 's' на конце", "Mask"),
                CheckParameter.Strings("singularEndings", "окончания на 's' в единственном числе", "ss", "us", "is"),
            ],
            Translate);

        private static IReadOnlyList<NamingRule> Translate(CheckSettings settings)
        {
            var pluralWords = settings.GetStrings("pluralWords");
            var singularEndings = settings.GetStrings("singularEndings");
            var flags = LegacyRules.Selector(new() { ["kinds"] = ["enum"], ["attributes"] = ["Flags"] });
            return
            [
                LegacyRules.Create(flags, NumberRequirement.Kind, new NumberRequirement(true, pluralWords, singularEndings)),
                LegacyRules.Create("enum", NumberRequirement.Kind, new NumberRequirement(false, pluralWords, singularEndings)),
            ];
        }
    }
}
