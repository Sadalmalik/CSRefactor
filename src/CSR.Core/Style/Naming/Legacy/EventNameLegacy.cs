using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Legacy
{
    // event-name до v0.4: маски имени события, запрещенные префиксы delegate -> правила naming
    public static class EventNameLegacy
    {
        public static readonly CheckAlias Check = new(
            "event-name",
            "имя события и delegate (устарела: naming)",
            LegacyRuleTools.Replacement,
            [
                CheckParameter.Strings("allowed", "маски допустимых имен события; пусто - любое имя", "On*"),
                CheckParameter.Strings("forbidden", "маски запрещенных имен события", "OnWill*", "OnDid*"),
                CheckParameter.Strings("delegatePrefixes", "запрещенные префиксы имени delegate (перед заглавной буквой)", "On"),
            ],
            settings => LegacyRuleTools.Analyze(Translate(settings)));

        private static IReadOnlyList<NamingRule> Translate(CheckSettings settings)
        {
            return
            [
                LegacyRuleTools.Rule(new()
                {
                    ["symbols"] = LegacyRuleTools.Symbols(new() { ["kinds"] = new[] { "event" } }),
                    ["allowed"] = settings.GetStrings("allowed"),
                    ["forbidden"] = settings.GetStrings("forbidden"),
                }),
                LegacyRuleTools.Rule(new()
                {
                    ["symbols"] = LegacyRuleTools.Symbols(new() { ["kinds"] = new[] { "delegate" } }),
                    ["forbiddenPrefixes"] = settings.GetStrings("delegatePrefixes"),
                }),
            ];
        }
    }
}
