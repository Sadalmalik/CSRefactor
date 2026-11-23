using CSR.Core.Checks;
using CSR.Core.Style.Naming.Requirements;

namespace CSR.Core.Style.Naming.Legacy
{
    // type-name до v0.4: префиксы интерфейса и параметра-типа, имена наследников -> правила naming
    public static class TypeNameLegacy
    {
        private static readonly IReadOnlyList<string> TypeKinds = ["class", "struct", "record", "interface"];

        public static readonly NamingAlias Check = new(
            "type-name",
            "префиксы интерфейсов и параметров-типов, имена наследников (устарела: naming)",
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
            Translate);

        private static IReadOnlyList<NamingRule> Translate(CheckSettings settings)
        {
            var rules = new List<NamingRule>();
            var interfacePrefix = settings.GetString("interfacePrefix");
            if (interfacePrefix.Length > 0)
            {
                var requirement = new PrefixRequirement(interfacePrefix, isWordStyle: true);
                rules.Add(LegacyRules.Create("interface", PrefixRequirement.Kind, requirement));
            }

            var typeParameterPrefix = settings.GetString("typeParameterPrefix");
            if (typeParameterPrefix.Length > 0)
            {
                var requirement = new PrefixRequirement(typeParameterPrefix, isWordStyle: true, isAloneAllowed: true);
                rules.Add(LegacyRules.Create("typeParameter", PrefixRequirement.Kind, requirement));
            }

            foreach (var pair in settings.GetMap("derivedNames"))
            {
                var symbols = LegacyRules.Selector(new() { ["kinds"] = TypeKinds, ["bases"] = [pair.Key] });
                rules.Add(LegacyRules.Create(symbols, MaskRequirement.Kind, new MaskRequirement([pair.Value], [])));
            }

            return rules;
        }
    }
}
