using CSR.Core.Checks;

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
            return
            [
                Rule(settings, new() { ["kinds"] = new[] { "enum" }, ["attributes"] = new[] { "Flags" } }, "plural"),
                Rule(settings, new() { ["kinds"] = new[] { "enum" } }, "singular"),
            ];
        }

        private static NamingRule Rule(CheckSettings settings, Dictionary<string, object> symbols, string number)
        {
            return LegacyRules.Rule(new()
            {
                ["symbols"] = LegacyRules.Symbols(symbols),
                ["number"] = number,
                ["pluralWords"] = settings.GetStrings("pluralWords"),
                ["singularEndings"] = settings.GetStrings("singularEndings"),
            });
        }
    }
}
