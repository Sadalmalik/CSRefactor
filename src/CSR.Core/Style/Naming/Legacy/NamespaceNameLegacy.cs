using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Legacy
{
    // namespace-name до v0.4: корень, запрещенные слова и глубина пространства имен -> правила naming
    public static class NamespaceNameLegacy
    {
        private static readonly string[] TypeKinds = ["class", "struct", "record", "interface", "enum"];

        public static readonly CheckAlias Check = new(
            "namespace-name",
            "корень, слова и глубина пространства имен (устарела: naming)",
            LegacyRuleTools.Replacement,
            [
                CheckParameter.Strings("roots", "допустимые корни (Company.Product); пусто - не проверять"),
                CheckParameter.Strings("forbiddenWords", "запрещенные слова в частях пространства (без учета регистра)"),
                CheckParameter.Int("maxDepth", 0, "предельное число частей; 0 - не проверять"),
            ],
            settings => LegacyRuleTools.Analyze(Translate(settings)));

        private static IReadOnlyList<NamingRule> Translate(CheckSettings settings)
        {
            var namespaces = new Dictionary<string, object>
            {
                ["symbols"] = LegacyRuleTools.Symbols(new() { ["kinds"] = new[] { "namespace" } }),
            };
            var roots = settings.GetStrings("roots");
            if (roots.Count > 0)
                namespaces["roots"] = roots;

            var words = settings.GetStrings("forbiddenWords");
            if (words.Count > 0)
                namespaces["forbiddenWords"] = words;

            int maxDepth = settings.GetInt("maxDepth");
            if (maxDepth > 0)
                namespaces["maxParts"] = maxDepth;

            var rules = new List<NamingRule>();
            if (namespaces.Count > 1)
                rules.Add(LegacyRuleTools.Rule(namespaces));

            if (roots.Count > 0)
            {
                rules.Add(LegacyRuleTools.Rule(new()
                {
                    ["symbols"] = LegacyRuleTools.Symbols(new() { ["kinds"] = TypeKinds }),
                    ["inNamespace"] = true,
                    ["subject"] = "тип",
                }));
            }

            return rules;
        }
    }
}
