namespace CSR.Core.Metrics
{
    // Покрытие строк одного файла: точки последовательности по строке и колонке начала.
    // Точка из нескольких отчетов (разные тестовые сборки) учитывается один раз и покрыта, если покрыта хоть в одном
    public class FileCoverage
    {
        private readonly Dictionary<int, Dictionary<int, bool>> m_Points = new();

        public void Add(int line, int column, bool isCovered)
        {
            if (!m_Points.TryGetValue(line, out Dictionary<int, bool>? points))
            {
                points = new Dictionary<int, bool>();
                m_Points[line] = points;
            }

            points.TryGetValue(column, out bool wasCovered);
            points[column] = wasCovered || isCovered;
        }

        // Процент покрытых точек в строках start..end; точек нет - 0, как в crapper
        public double GetPercent(int start, int end)
        {
            var total = 0;
            var covered = 0;
            for (var line = start; line <= end; line++)
            {
                if (!m_Points.TryGetValue(line, out Dictionary<int, bool>? points))
                    continue;

                total += points.Count;
                covered += points.Values.Count(isCovered => isCovered);
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
                function.Crap = Round(Score(function.Values[MetricKey.Cyclomatic], percent));
            }
        }
    }
}
