using System.Text.Json;
using CSR.Core.Metrics;

namespace CSR.Reports.Config
{
    // metrics.jsonc:
    // { "top": 10, "function": { "cyclomatic": { "threshold": 10 }, "code-lines": { "enabled": false } },
    //   "type": {}, "file": {} }
    public static class MetricsConfig
    {
        public static MetricsOptions Parse(string json)
        {
            using var document = JsonConfig.Parse(json);
            var root = document.RootElement;
            JsonConfig.RequireObject(root, "metrics");

            var options = new MetricsOptions();
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == "top")
                {
                    options.Top = OptionKind.Count.ReadJson(property.Value, "top");
                    continue;
                }

                MetricLevel? level = FindLevel(property.Name);
                if (level == null)
                    throw new ConfigException($"неизвестный ключ '{property.Name}'");

                ReadLevel(options, level.Value, property.Value);
            }

            return options;
        }

        private static void ReadLevel(MetricsOptions options, MetricLevel level, JsonElement element)
        {
            var levelKey = MetricCatalog.GetLevelKey(level);
            JsonConfig.RequireObject(element, levelKey);
            foreach (var property in element.EnumerateObject())
            {
                MetricDefinition? metric = MetricCatalog.All.FirstOrDefault(
                    definition => definition.Level == level && definition.Key == property.Name);
                if (metric == null)
                    throw new ConfigException($"{levelKey}: неизвестная метрика '{property.Name}'");

                ReadMetric(options, metric, property.Value);
            }
        }

        private static void ReadMetric(MetricsOptions options, MetricDefinition metric, JsonElement element)
        {
            JsonConfig.RequireObject(element, metric.Id);
            foreach (var property in element.EnumerateObject())
            {
                var path = metric.Id + "." + property.Name;
                switch (property.Name)
                {
                    case "enabled":
                        options.SetEnabled(metric, JsonConfig.ReadBool(property.Value, path));
                        break;
                    case "threshold":
                        int? threshold = property.Value.ValueKind == JsonValueKind.Null
                            ? null
                            : OptionKind.Integer.ReadJson(property.Value, path);
                        options.SetThreshold(metric, threshold);
                        break;
                    default:
                        throw new ConfigException($"{metric.Id}: неизвестный ключ '{property.Name}'");
                }
            }
        }

        private static MetricLevel? FindLevel(string key)
        {
            foreach (var level in Enum.GetValues<MetricLevel>())
            {
                if (MetricCatalog.GetLevelKey(level) == key)
                    return level;
            }

            return null;
        }
    }
}
