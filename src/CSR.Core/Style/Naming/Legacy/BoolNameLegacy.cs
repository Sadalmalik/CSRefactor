using CSR.Core.Checks;
using CSR.Core.Style.Naming.Requirements;

namespace CSR.Core.Style.Naming.Legacy
{
    // bool-name до v0.4: отрицание в имени bool -> правило naming с подлежащим "bool"
    public static class BoolNameLegacy
    {
        public static readonly NamingAlias Check = new(
            "bool-name",
            "отрицание в имени bool (устарела: naming)",
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
            Translate);

        private static IReadOnlyList<NamingRule> Translate(CheckSettings settings)
        {
            var symbols = LegacyRules.Selector(new()
            {
                ["kinds"] = settings.GetStrings("members"),
                ["types"] = ["bool", "Boolean"],
            });
            var requirement = new WordsRequirement(
                settings.GetStrings("forbiddenWords"),
                settings.GetStrings("negationPrefixes"),
                settings.GetStrings("ignoreWords"),
                true);
            return [LegacyRules.Create(symbols, WordsRequirement.Kind, requirement, "bool")];
        }
    }
}
