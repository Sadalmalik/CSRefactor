using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Legacy
{
    // field-prefix до v0.4: префиксы полей и регистр констант -> правила naming. Порядок правил - каскад: константа,
    // поля без префикса по доступу, static readonly, статическое поле, поле, свойства
    public static class FieldPrefixLegacy
    {
        private static readonly string[] AllAccess = ["private", "protected", "internal", "public"];
        private static readonly string[] Field = ["field"];

        public static readonly CheckAlias Check = new(
            "field-prefix",
            "префиксы полей и регистр констант (устарела: naming)",
            LegacyRules.Replacement,
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
            settings => LegacyRules.Analyze(Translate(settings)));

        private static IReadOnlyList<NamingRule> Translate(CheckSettings settings)
        {
            var known = new[] { settings.GetString("instancePrefix"), settings.GetString("staticPrefix") };
            var prefixedAccess = settings.GetStrings("prefixedAccess");
            var constants = settings.GetString("constants");
            var constant = new Dictionary<string, object>
            {
                ["symbols"] = LegacyRules.Symbols(new() { ["kinds"] = Field, ["modifiers"] = new[] { "const" } }),
            };
            if (constants == "any")
                constant["skip"] = new[] { "case", "prefix" };
            else
            {
                constant["case"] = constants;
                constant["skip"] = new[] { "prefix" };
            }

            var rules = new List<NamingRule> { LegacyRules.Rule(constant) };
            var openAccess = AllAccess.Where(access => !prefixedAccess.Contains(access)).ToArray();
            if (openAccess.Length > 0)
                rules.Add(Unprefixed(new() { ["kinds"] = Field, ["access"] = openAccess }, known, "поле"));

            if (settings.GetString("staticReadonly") == "pascal")
                rules.Add(
                    Unprefixed(
                        new() { ["kinds"] = Field, ["modifiers"] = new[] { "static", "readonly" } },
                        known,
                        "static readonly"));

            rules.Add(Prefixed(settings, new() { ["kinds"] = Field, ["modifiers"] = new[] { "static" } }, "staticPrefix", known));
            rules.Add(Prefixed(settings, new() { ["kinds"] = Field }, "instancePrefix", known));
            if (settings.GetBool("properties"))
                rules.Add(Unprefixed(new() { ["kinds"] = new[] { "property" } }, known, null));

            return rules;
        }

        // Поле с префиксом параметра; пустой префикс - без префикса полей
        private static NamingRule Prefixed(
            CheckSettings settings,
            Dictionary<string, object> symbols,
            string parameter,
            string[] known)
        {
            var prefix = settings.GetString(parameter);
            if (prefix.Length == 0)
                return Unprefixed(symbols, known, null);

            return LegacyRules.Rule(new()
            {
                ["symbols"] = LegacyRules.Symbols(symbols),
                ["prefix"] = prefix,
                ["pascalAfterPrefix"] = settings.GetBool("pascalAfterPrefix"),
            });
        }

        private static NamingRule Unprefixed(Dictionary<string, object> symbols, string[] known, string? subject)
        {
            var fields = new Dictionary<string, object>
            {
                ["symbols"] = LegacyRules.Symbols(symbols),
                ["prefix"] = "",
                ["knownPrefixes"] = known,
            };
            if (subject != null)
                fields["subject"] = subject;

            return LegacyRules.Rule(fields);
        }
    }
}
