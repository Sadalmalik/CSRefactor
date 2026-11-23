using System.Text.Json;
using CSR.Core.Similar;

namespace CSR.Reports.Config
{
    // similar.jsonc:
    // { "threshold": 0.82, "minLines": 4, "minNodes": 20, "minShapes": 20, "fragmentStatements": 4,
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
                    case "threshold":
                        options.Threshold = ReadThreshold(value, property.Name);
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
