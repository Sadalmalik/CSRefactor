using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Legacy
{
    // event-name до v0.4: маски имени события, запрещенные префиксы delegate -> правила naming
    public static class EventNameLegacy
    {
        public static readonly NamingAlias Check = new(
            "event-name",
            "имя события и delegate (устарела: naming)",
            [
                CheckParameter.Strings("allowed", "маски допустимых имен события; пусто - любое имя", "On*"),
                CheckParameter.Strings("forbidden", "маски запрещенных имен события", "OnWill*", "OnDid*"),
                CheckParameter.Strings("delegatePrefixes", "запрещенные префиксы имени delegate (перед заглавной буквой)", "On"),
            ],
            Translate);

        private static IReadOnlyList<NamingRule> Translate(CheckSettings settings)
        {
            return
            [
                LegacyRules.Rule(new()
                {
                    ["symbols"] = LegacyRules.Symbols(new() { ["kinds"] = new[] { "event" } }),
                    ["allowed"] = settings.GetStrings("allowed"),
                    ["forbidden"] = settings.GetStrings("forbidden"),
                }),
                LegacyRules.Rule(new()
                {
                    ["symbols"] = LegacyRules.Symbols(new() { ["kinds"] = new[] { "delegate" } }),
                    ["forbiddenPrefixes"] = settings.GetStrings("delegatePrefixes"),
                }),
            ];
        }
    }
}
