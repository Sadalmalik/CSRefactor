namespace CSR.Core.Metrics
{
    // Настройки отчета метрик: размер топа, включение метрик и пороги; по умолчанию - MetricCatalog
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
    }
}
