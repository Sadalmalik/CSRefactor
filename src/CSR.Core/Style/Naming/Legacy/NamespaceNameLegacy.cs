using CSR.Core.Checks;
using CSR.Core.Style.Naming.Requirements;

namespace CSR.Core.Style.Naming.Legacy
{
    // namespace-name до v0.4: корень, запрещенные слова и глубина пространства имен -> правила naming
    public static class NamespaceNameLegacy
    {
        private static readonly IReadOnlyList<string> TypeKinds = ["class", "struct", "record", "interface", "enum"];

        public static readonly NamingAlias Check = new(
            "namespace-name",
            "корень, слова и глубина пространства имен (устарела: naming)",
            [
                CheckParameter.Strings("roots", "допустимые корни (Company.Product); пусто - не проверять"),
                CheckParameter.Strings("forbiddenWords", "запрещенные слова в частях пространства (без учета регистра)"),
                CheckParameter.Int("maxDepth", 0, "предельное число частей; 0 - не проверять"),
            ],
            Translate);

        private static IReadOnlyList<NamingRule> Translate(CheckSettings settings)
        {
            var rules = new List<NamingRule>();
            var roots = settings.GetStrings("roots");
            if (roots.Count > 0)
            {
                var types = LegacyRules.Selector(new() { ["kinds"] = TypeKinds });
                rules.Add(LegacyRules.Create("namespace", NamespaceRequirement.Kind, new NamespaceRequirement(roots)));
                rules.Add(LegacyRules.Create(types, NamespaceRequirement.Kind, new NamespaceRequirement(roots), "тип"));
            }

            var words = settings.GetStrings("forbiddenWords");
            if (words.Count > 0)
                rules.Add(LegacyRules.Create("namespace", WordsRequirement.Kind, new WordsRequirement(words, [], [], false)));

            int maxDepth = settings.GetInt("maxDepth");
            if (maxDepth > 0)
                rules.Add(LegacyRules.Create("namespace", DepthRequirement.Kind, new DepthRequirement(maxDepth)));

            return rules;
        }
    }
}
