using System.Text.Json;
using CSR.Core.Config;

namespace CSR.Core.Metrics
{
    // Настройки отчета метрик (metrics.jsonc):
    // { "top": 10, "function": { "cyclomatic": { "threshold": 10 }, "code-lines": { "enabled": false } },
    //   "type": {}, "file": {} }
    public class MetricsOptions
    {
        private readonly Dictionary<string, bool> m_Enabled = new();
        private readonly Dictionary<string, int?> m_Thresholds = new();

        public int Top { get; set; } = 10;

        public bool IsEnabled(MetricDefinition metric)
            => !m_Enabled.TryGetValue(metric.Id, out bool enabled) || enabled;

        public int? GetThreshold(MetricDefinition metric)
            => m_Thresholds.TryGetValue(metric.Id, out int? threshold) ? threshold : metric.DefaultThreshold;

        public void SetEnabled(MetricDefinition metric, bool enabled)
            => m_Enabled[metric.Id] = enabled;

        public void SetThreshold(MetricDefinition metric, int? threshold)
            => m_Thresholds[metric.Id] = threshold;

        public static MetricsOptions FromJson(string json)
        {
            using JsonDocument document = JsonConfig.Parse(json);
            JsonElement root = document.RootElement;
            JsonConfig.RequireObject(root, "metrics");

            var options = new MetricsOptions();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.Name == "top")
                {
                    options.Top = JsonConfig.ReadInt(property.Value, "top");
                    continue;
                }

                MetricLevel? level = FindLevel(property.Name);
                if (level == null)
                    throw new ConfigException($"неизвестный ключ '{property.Name}'");

                options.ReadLevel(level.Value, property.Value);
            }

            return options;
        }

        private void ReadLevel(MetricLevel level, JsonElement element)
        {
            string levelKey = MetricCatalog.GetLevelKey(level);
            JsonConfig.RequireObject(element, levelKey);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                MetricDefinition? metric = MetricCatalog.All.FirstOrDefault(
                    definition => definition.Level == level && definition.Key == property.Name);
                if (metric == null)
                    throw new ConfigException($"{levelKey}: неизвестная метрика '{property.Name}'");

                ReadMetric(metric, property.Value);
            }
        }

        private void ReadMetric(MetricDefinition metric, JsonElement element)
        {
            JsonConfig.RequireObject(element, metric.Id);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                string path = metric.Id + "." + property.Name;
                switch (property.Name)
                {
                    case "enabled":
                        SetEnabled(metric, JsonConfig.ReadBool(property.Value, path));
                        break;
                    case "threshold":
                        int? threshold = property.Value.ValueKind == JsonValueKind.Null
                            ? null
                            : JsonConfig.ReadInt(property.Value, path);
                        SetThreshold(metric, threshold);
                        break;
                    default:
                        throw new ConfigException($"{metric.Id}: неизвестный ключ '{property.Name}'");
                }
            }
        }

        private static MetricLevel? FindLevel(string key)
        {
            foreach (MetricLevel level in Enum.GetValues<MetricLevel>())
            {
                if (MetricCatalog.GetLevelKey(level) == key)
                    return level;
            }

            return null;
        }
    }
}
