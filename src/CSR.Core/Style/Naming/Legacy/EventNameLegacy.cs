using CSR.Core.Checks;
using CSR.Core.Style.Naming.Requirements;

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
            var events = new MaskRequirement(settings.GetStrings("allowed"), settings.GetStrings("forbidden"));
            var delegates = new PrefixRequirement(null, forbidden: settings.GetStrings("delegatePrefixes"));
            return
            [
                LegacyRules.Create("event", MaskRequirement.Kind, events),
                LegacyRules.Create("delegate", PrefixRequirement.Kind, delegates),
            ];
        }
    }
}
