using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Legacy
{
    // bool-name до v0.4: отрицание в имени bool -> правило naming с подлежащим "bool"
    public static class BoolNameLegacy
    {
        public static readonly CheckAlias Check = new(
            "bool-name",
            "отрицание в имени bool (устарела: naming)",
            LegacyRuleTools.Replacement,
            [
                CheckParameter.Strings(
                    "members",
                    "виды: field, property, parameter, local",
                    "field",
                    "property",
                    "parameter",
                    "local"),
                CheckParameter.Strings(
                    "forbiddenWords",
                    "запрещенные слова имени (без учета регистра)",
                    "Not",
                    "No",
                    "Non",
                    "Dont"),
                CheckParameter.Strings(
                    "negationPrefixes",
                    "приставки отрицания: слово имени, которое с нее начинается (Dis - Disabled)"),
                CheckParameter.Strings("ignoreWords", "слова с приставкой из negationPrefixes, которые не отрицание (Display)"),
            ],
            settings => LegacyRuleTools.Analyze(Translate(settings)));

        private static IReadOnlyList<NamingRule> Translate(CheckSettings settings)
        {
            var fields = new Dictionary<string, object>
            {
                ["symbols"] = LegacyRuleTools.Symbols(new()
                {
                    ["kinds"] = LegacyRuleTools.KnownKinds(settings.GetStrings("members")),
                    ["types"] = new[] { "bool", "Boolean" },
                }),
                ["forbiddenWords"] = settings.GetStrings("forbiddenWords"),
                ["negationPrefixes"] = settings.GetStrings("negationPrefixes"),
                ["ignoreWords"] = settings.GetStrings("ignoreWords"),
                ["negation"] = true,
                ["subject"] = "bool",
            };
            return [LegacyRuleTools.Rule(fields)];
        }
    }
}
