using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Legacy
{
    // collection-name до v0.4: коллекция с именем в единственном числе -> правило naming с подлежащим "коллекция"
    public static class CollectionNameLegacy
    {
        public static readonly CheckAlias Check = new(
            "collection-name",
            "коллекция с именем в единственном числе (устарела: naming)",
            LegacyRuleTools.Replacement,
            [
                CheckParameter.Strings("members", "виды: field, property, parameter, local", "field", "property"),
                CheckParameter.Strings(
                    "collectionTypes",
                    "маски типов-коллекций (без пространства имен)",
                    "List<*>",
                    "IList<*>",
                    "IReadOnlyList<*>",
                    "ICollection<*>",
                    "IReadOnlyCollection<*>",
                    "IEnumerable<*>",
                    "HashSet<*>",
                    "ISet<*>",
                    "IReadOnlySet<*>",
                    "SortedSet<*>",
                    "Queue<*>",
                    "Stack<*>",
                    "LinkedList<*>",
                    "Dictionary<*>",
                    "IDictionary<*>",
                    "IReadOnlyDictionary<*>",
                    "SortedDictionary<*>",
                    "ConcurrentDictionary<*>"),
                CheckParameter.Bool("arrays", true, "проверять массивы"),
                CheckParameter.Strings(
                    "pluralWords",
                    "последние слова, которые считаются множественным числом или постфиксом коллекции",
                    "List",
                    "Array",
                    "Dict",
                    "Dictionary",
                    "Set",
                    "Map",
                    "Queue",
                    "Stack",
                    "Collection",
                    "Lookup",
                    "Table",
                    "Index",
                    "Registry",
                    "Cache",
                    "Pool",
                    "Buffer",
                    "History",
                    "All",
                    "Data",
                    "Children",
                    "People"),
                CheckParameter.Strings("singularEndings", "окончания на 's' в единственном числе", "ss", "us", "is"),
            ],
            settings => LegacyRuleTools.Analyze(Translate(settings)));

        private static IReadOnlyList<NamingRule> Translate(CheckSettings settings)
        {
            var types = settings.GetStrings("collectionTypes").ToList();
            if (settings.GetBool("arrays"))
                types.Add("*[*]");

            var fields = new Dictionary<string, object>
            {
                ["symbols"] = LegacyRuleTools.Symbols(new()
                {
                    ["kinds"] = LegacyRuleTools.KnownKinds(settings.GetStrings("members")),
                    ["types"] = types,
                }),
                ["number"] = "plural",
                ["pluralWords"] = settings.GetStrings("pluralWords"),
                ["singularEndings"] = settings.GetStrings("singularEndings"),
                ["subject"] = "коллекция",
            };
            return [LegacyRuleTools.Rule(fields)];
        }
    }
}
