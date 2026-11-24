using System.Text.Json;
using CSR.Core.Similar;

namespace CSR.Reports.Config
{
    // similar.jsonc:
    // { "kinds": ["member", "type"], "threshold": 0.82, "minLines": 4, "minNodes": 20, "minShapes": 20,
    //   "fragmentStatements": 4, "setThreshold": 0.5, "minShared": 3, "synonyms": { "pos": "position" },
    //   "typeThreshold": 0.75, "minTypeMembers": 4, "minCoreMembers": 4, "weights": { "bases": 1, "bodies": 2 },
    //   "paths": ["Assets/Code/*"], "excludePaths": ["*/Tests/*", "*.g.cs"] }
    public static class SimilarConfig
    {
        // Виды единиц: массив в конфиге, список через запятую в командной строке
        private static readonly OptionKind<HashSet<UnitKind>> Kinds = new()
        {
            ReadJson = (element, path) => ToKinds(JsonConfig.ReadStrings(element, path), path),
            ParseText = (text, name)
                => ToKinds(text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), name),
        };

        // Синонимы: имя -> имя, в нижнем регистре
        private static readonly OptionKind<Dictionary<string, string>> Synonyms = new() { ReadJson = ReadSynonyms };

        private static readonly OptionTable<TypeWeights> Weights = new OptionTable<TypeWeights>()
            .Add("bases", OptionKinds.Weight, (weights, value) => weights.Bases = value)
            .Add("overrides", OptionKinds.Weight, (weights, value) => weights.Overrides = value)
            .Add("fields", OptionKinds.Weight, (weights, value) => weights.Fields = value)
            .Add("signatures", OptionKinds.Weight, (weights, value) => weights.Signatures = value)
            .Add("bodies", OptionKinds.Weight, (weights, value) => weights.Bodies = value);

        public static readonly OptionTable<SimilarOptions> Table = new OptionTable<SimilarOptions>()
            .Add("kinds", Kinds, (options, value) => options.Kinds = value, "--kinds")
            .Add("threshold", OptionKinds.Share, (options, value) => options.Threshold = value, "--threshold")
            .Add("minLines", OptionKinds.Count, (options, value) => options.MinLines = value, "--min-lines")
            .Add("minNodes", OptionKinds.Count, (options, value) => options.MinNodes = value, "--min-nodes")
            .Add("minShapes", OptionKinds.Count, (options, value) => options.MinShapes = value, "--min-shapes")
            .Add("fragmentStatements", OptionKinds.Count, (options, value) => options.FragmentStatements = value, "--fragments")
            .Add("setThreshold", OptionKinds.Share, (options, value) => options.SetThreshold = value, "--set-threshold")
            .Add("minShared", OptionKinds.Count, (options, value) => options.MinShared = value, "--min-shared")
            .Add("synonyms", Synonyms, (options, value) => options.Synonyms = value)
            .Add("typeThreshold", OptionKinds.Share, (options, value) => options.TypeThreshold = value, "--type-threshold")
            .Add("minTypeMembers", OptionKinds.Count, (options, value) => options.MinTypeMembers = value)
            .Add("minCoreMembers", OptionKinds.Count, (options, value) => options.MinCoreMembers = value)
            .AddSection("weights", Weights, options => options.Weights)
            .Add("systemThreshold", OptionKinds.Share, (options, value) => options.SystemThreshold = value, "--system-threshold")
            .Add("minSystemTypes", OptionKinds.Count, (options, value) => options.MinSystemTypes = value)
            .Add("paths", OptionKinds.Strings, (options, value) => options.Paths = value)
            .Add("excludePaths", OptionKinds.Strings, (options, value) => options.ExcludePaths = value);

        public static SimilarOptions Parse(string json)
        {
            var options = new SimilarOptions();
            Table.ReadRoot(json, "similar", options);
            return options;
        }

        private static HashSet<UnitKind> ToKinds(IEnumerable<string> keys, string name)
        {
            var kinds = new HashSet<UnitKind>();
            foreach (var key in keys)
            {
                var kind = UnitKinds.Find(key);
                if (kind == null)
                    throw new ConfigException(
                        $"{name}: неизвестный вид '{key}'; допустимо {string.Join(", ", UnitKinds.AllKeys)}");

                kinds.Add(kind.Value);
            }

            return kinds;
        }

        private static Dictionary<string, string> ReadSynonyms(JsonElement value, string path)
        {
            JsonConfig.RequireObject(value, path);
            var synonyms = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                    throw new ConfigException($"{path}.{property.Name}: ожидается строка");

                synonyms[property.Name.ToLowerInvariant()] = property.Value.GetString()!.ToLowerInvariant();
            }

            return synonyms;
        }
    }
}
