using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Legacy
{
    // type-name до v0.4: префиксы интерфейса и параметра-типа, имена наследников -> правила naming
    public static class TypeNameLegacy
    {
        private static readonly string[] TypeKinds = ["class", "struct", "record", "interface"];

        public static readonly CheckAlias Check = new(
            "type-name",
            "префиксы интерфейсов и параметров-типов, имена наследников (устарела: naming)",
            LegacyRuleTools.Replacement,
            [
                CheckParameter.Text("interfacePrefix", "I", "префикс интерфейса перед заглавной буквой; пусто - не проверять"),
                CheckParameter.Text(
                    "typeParameterPrefix",
                    "T",
                    "префикс параметра-типа (T или перед заглавной буквой); пусто - не проверять"),
                CheckParameter.Map(
                    "derivedNames",
                    new Dictionary<string, string>(),
                    "маска имени базового типа -> маска имени наследника (\"*Command\": \"*Command\")"),
            ],
            settings => LegacyRuleTools.Analyze(Translate(settings)));

        private static IReadOnlyList<NamingRule> Translate(CheckSettings settings)
        {
            var rules = new List<NamingRule>();
            var interfacePrefix = settings.GetString("interfacePrefix");
            if (interfacePrefix.Length > 0)
            {
                rules.Add(LegacyRuleTools.Rule(new()
                {
                    ["symbols"] = LegacyRuleTools.Symbols(new() { ["kinds"] = new[] { "interface" } }),
                    ["prefix"] = interfacePrefix,
                    ["prefixStyle"] = "word",
                }));
            }

            var typeParameterPrefix = settings.GetString("typeParameterPrefix");
            if (typeParameterPrefix.Length > 0)
            {
                rules.Add(LegacyRuleTools.Rule(new()
                {
                    ["symbols"] = LegacyRuleTools.Symbols(new() { ["kinds"] = new[] { "typeParameter" } }),
                    ["prefix"] = typeParameterPrefix,
                    ["prefixStyle"] = "word",
                    ["prefixAlone"] = true,
                }));
            }

            foreach (var pair in settings.GetMap("derivedNames"))
            {
                rules.Add(LegacyRuleTools.Rule(new()
                {
                    ["symbols"] = LegacyRuleTools.Symbols(new() { ["kinds"] = TypeKinds, ["bases"] = new[] { pair.Key } }),
                    ["allowed"] = new[] { pair.Value },
                }));
            }

            return rules;
        }
    }
}
