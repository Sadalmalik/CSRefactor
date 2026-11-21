namespace CSR.Core.Metrics
{
    // Покрытие строк одного файла: на строке - число точек последовательности и покрытых из них
    public class FileCoverage
    {
        private readonly Dictionary<int, int> m_Total = new();
        private readonly Dictionary<int, int> m_Covered = new();

        public void Add(int line, bool isCovered)
        {
            m_Total.TryGetValue(line, out int total);
            m_Total[line] = total + 1;
            if (!isCovered)
                return;

            m_Covered.TryGetValue(line, out int covered);
            m_Covered[line] = covered + 1;
        }

        // Процент покрытых точек в строках start..end; точек нет - 0, как в crapper
        public double GetPercent(int start, int end)
        {
            var total = 0;
            var covered = 0;
            for (var line = start; line <= end; line++)
            {
                if (!m_Total.TryGetValue(line, out int lineTotal))
                    continue;

                m_Covered.TryGetValue(line, out int lineCovered);
                total += lineTotal;
                covered += lineCovered;
            }

            return total == 0 ? 0.0 : 100.0 * covered / total;
        }
    }

    // CRAP = CC^2 * (1 - покрытие)^3 + CC, как в crapper: покрытие в процентах, значения округляются до 4 знаков
    public static class Crap
    {
        public static double Score(int complexity, double coveragePercent)
        {
            double cc = complexity;
            var uncovered = 1.0 - coveragePercent / 100.0;
            return cc * cc * uncovered * uncovered * uncovered + cc;
        }

        public static double Round(double value)
            => Math.Round(value, 4, MidpointRounding.ToEven);

        // Покрытие и CRAP функций файла; функция вне отчета покрытия (coverage == null) - покрытие 0
        public static void Apply(FileMetrics file, FileCoverage? coverage)
        {
            foreach (var function in file.Functions)
            {
                var percent = Round(coverage?.GetPercent(function.Line, function.EndLine) ?? 0.0);
                function.Coverage = percent;
                function.Crap = Round(Score(function.Values[MetricKeys.Cyclomatic], percent));
            }
        }
    }
}
