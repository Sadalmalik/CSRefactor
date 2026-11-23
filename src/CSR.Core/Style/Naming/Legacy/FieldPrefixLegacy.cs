using CSR.Core.Checks;
using CSR.Core.Style.Naming.Requirements;

namespace CSR.Core.Style.Naming.Legacy
{
    // field-prefix до v0.4: префиксы полей и регистр констант -> правила naming. Порядок правил - каскад: константа,
    // поля без префикса по доступу, static readonly, статическое поле, поле, свойства
    public static class FieldPrefixLegacy
    {
        private static readonly IReadOnlyList<string> AllAccess = ["private", "protected", "internal", "public"];

        public static readonly NamingAlias Check = new(
            "field-prefix",
            "префиксы полей и регистр констант (устарела: naming)",
            [
                CheckParameter.Text("instancePrefix", "m_", "префикс закрытого поля экземпляра; пусто - без префикса"),
                CheckParameter.Text("staticPrefix", "s_", "префикс закрытого статического поля; пусто - без префикса"),
                CheckParameter.Choice(
                    "staticReadonly",
                    "закрытое static readonly: static - префикс staticPrefix, pascal - без префикса, как константа",
                    "static",
                    "pascal"),
                CheckParameter.Choice(
                    "constants",
                    "регистр константы: pascal - MaxSize, upper - MAX_SIZE, any - не проверять",
                    "pascal",
                    "upper",
                    "any"),
                CheckParameter.Strings(
                    "prefixedAccess",
                    "доступ полей с префиксами: private (и без модификатора), protected, internal, public",
                    "private"),
                CheckParameter.Bool("pascalAfterPrefix", true, "после префикса - заглавная буква (m_Name, не m_name)"),
                CheckParameter.Bool("properties", true, "свойства без префиксов полей"),
            ],
            Translate);

        private static IReadOnlyList<NamingRule> Translate(CheckSettings settings)
        {
            var instancePrefix = settings.GetString("instancePrefix");
            var staticPrefix = settings.GetString("staticPrefix");
            bool isPascalAfterPrefix = settings.GetBool("pascalAfterPrefix");
            IReadOnlyList<string> known = [instancePrefix, staticPrefix];
            var prefixedAccess = settings.GetStrings("prefixedAccess");
            var unprefixed = new PrefixRequirement("", known: known);
            var rules = new List<NamingRule>
            {
                Rule(
                    new() { ["kinds"] = ["field"], ["modifiers"] = ["const"] },
                    new()
                    {
                        [CaseRequirement.Kind.Key] = new CaseRequirement(settings.GetString("constants")),
                        [PrefixRequirement.Kind.Key] = new PrefixRequirement(PrefixRequirement.Any),
                    }),
            };

            var openAccess = AllAccess.Where(access => !prefixedAccess.Contains(access)).ToList();
            if (openAccess.Count > 0)
                rules.Add(Rule(new() { ["kinds"] = ["field"], ["access"] = openAccess }, unprefixed, "поле"));

            if (settings.GetString("staticReadonly") == "pascal")
                rules.Add(
                    Rule(new() { ["kinds"] = ["field"], ["modifiers"] = ["static", "readonly"] }, unprefixed, "static readonly"));

            var staticRequirement = new PrefixRequirement(staticPrefix, isPascalAfterPrefix: isPascalAfterPrefix, known: known);
            var instanceRequirement = new PrefixRequirement(
                instancePrefix,
                isPascalAfterPrefix: isPascalAfterPrefix,
                known: known);
            rules.Add(Rule(new() { ["kinds"] = ["field"], ["modifiers"] = ["static"] }, staticRequirement));
            rules.Add(Rule(new() { ["kinds"] = ["field"] }, instanceRequirement));
            if (settings.GetBool("properties"))
                rules.Add(Rule(new() { ["kinds"] = ["property"] }, unprefixed));

            return rules;
        }

        private static NamingRule Rule(
            Dictionary<string, IReadOnlyList<string>> symbols,
            PrefixRequirement requirement,
            string? label = null)
        {
            return LegacyRules.Create(LegacyRules.Selector(symbols), PrefixRequirement.Kind, requirement, label);
        }

        private static NamingRule Rule(
            Dictionary<string, IReadOnlyList<string>> symbols,
            Dictionary<string, INameRequirement> requirements)
        {
            return new NamingRule(LegacyRules.Selector(symbols), requirements);
        }
    }
}
