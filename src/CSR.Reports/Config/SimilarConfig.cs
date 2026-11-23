using System.Text.Json;
using CSR.Core.Similar;

namespace CSR.Reports.Config
{
    // similar.jsonc:
    // { "kinds": ["member", "type"], "threshold": 0.82, "minLines": 4, "minNodes": 20, "minShapes": 20,
    //   "fragmentStatements": 4, "setThreshold": 0.5, "minShared": 3, "synonyms": { "pos": "position" },
    //   "typeThreshold": 0.75, "minTypeMembers": 4, "weights": { "bases": 1, "bodies": 2 },
    //   "paths": ["Assets/Code/*"], "excludePaths": ["*/Tests/*", "*.g.cs"] }
    public static class SimilarConfig
    {
        public static SimilarOptions Parse(string json)
        {
            using var document = JsonConfig.Parse(json);
            var root = document.RootElement;
            JsonConfig.RequireObject(root, "similar");

            var options = new SimilarOptions();
            foreach (var property in root.EnumerateObject())
            {
                var value = property.Value;
                switch (property.Name)
                {
                    case "kinds":
                        options.Kinds = ReadKinds(value, property.Name);
                        break;
                    case "threshold":
                        options.Threshold = ReadThreshold(value, property.Name);
                        break;
                    case "setThreshold":
                        options.SetThreshold = ReadThreshold(value, property.Name);
                        break;
                    case "minShared":
                        options.MinShared = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "synonyms":
                        options.Synonyms = ReadSynonyms(value, property.Name);
                        break;
                    case "typeThreshold":
                        options.TypeThreshold = ReadThreshold(value, property.Name);
                        break;
                    case "minTypeMembers":
                        options.MinTypeMembers = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "weights":
                        options.Weights = ReadWeights(value, property.Name);
                        break;
                    case "minLines":
                        options.MinLines = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "minNodes":
                        options.MinNodes = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "minShapes":
                        options.MinShapes = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "fragmentStatements":
                        options.FragmentStatements = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "paths":
                        options.Paths = JsonConfig.ReadStrings(value, property.Name);
                        break;
                    case "excludePaths":
                        options.ExcludePaths = JsonConfig.ReadStrings(value, property.Name);
                        break;
                    default:
                        throw new ConfigException($"неизвестный ключ '{property.Name}'");
                }
            }

            return options;
        }

        private static HashSet<UnitKind> ReadKinds(JsonElement value, string path)
        {
            var kinds = new HashSet<UnitKind>();
            foreach (var key in JsonConfig.ReadStrings(value, path))
            {
                var kind = UnitKinds.Find(key);
                if (kind == null)
                    throw new ConfigException(
                        $"{path}: неизвестный вид '{key}'; допустимо {string.Join(", ", UnitKinds.AllKeys)}");

                kinds.Add(kind.Value);
            }

            return kinds;
        }

        // Синонимы: имя -> имя, в нижнем регистре
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

        private static TypeWeights ReadWeights(JsonElement value, string path)
        {
            JsonConfig.RequireObject(value, path);
            var weights = new TypeWeights();
            foreach (var property in value.EnumerateObject())
            {
                var itemPath = path + "." + property.Name;
                if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDouble(out double weight)
                    || weight < 0)
                    throw new ConfigException($"{itemPath}: ожидается неотрицательное число");

                switch (property.Name)
                {
                    case "bases":
                        weights.Bases = weight;
                        break;
                    case "overrides":
                        weights.Overrides = weight;
                        break;
                    case "fields":
                        weights.Fields = weight;
                        break;
                    case "signatures":
                        weights.Signatures = weight;
                        break;
                    case "bodies":
                        weights.Bodies = weight;
                        break;
                    default:
                        throw new ConfigException($"{path}: неизвестный ключ '{property.Name}'");
                }
            }

            return weights;
        }

        // Порог - число от 0 до 1
        private static double ReadThreshold(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double threshold)
                || threshold is < 0 or > 1)
                throw new ConfigException($"{path}: ожидается число от 0 до 1");

            return threshold;
        }
    }
}
