using System.Xml.Linq;
using CSR.Core.Metrics;

namespace CSR.Reports.Metrics
{
    // Покрытие из OpenCover XML (Unity Code Coverage, coverlet, OpenCover): точки последовательности
    // SequencePoint (vc - число выполнений, sl / sc - строка и колонка, fileid - File uid) по файлам Files/File.
    // Несколько отчетов объединяются в один результат
    public static class OpenCoverReader
    {
        // Ключ - полный путь файла из отчета, без учета регистра
        public static Dictionary<string, FileCoverage> Read(string xml)
        {
            var result = new Dictionary<string, FileCoverage>(StringComparer.OrdinalIgnoreCase);
            Read(xml, result);
            return result;
        }

        public static void Read(string xml, Dictionary<string, FileCoverage> result)
        {
            var document = XDocument.Parse(xml);
            foreach (var module in document.Descendants("Module"))
            {
                var paths = module.Descendants("File")
                    .Where(file => file.Attribute("uid") != null && file.Attribute("fullPath") != null)
                    .GroupBy(file => file.Attribute("uid")!.Value)
                    .ToDictionary(group => group.Key, group => group.First().Attribute("fullPath")!.Value);

                foreach (var point in module.Descendants("SequencePoint"))
                    AddPoint(result, paths, point);
            }
        }

        private static void AddPoint(Dictionary<string, FileCoverage> result, Dictionary<string, string> paths,
            XElement point)
        {
            var fileId = point.Attribute("fileid")?.Value;
            if (fileId == null || !paths.TryGetValue(fileId, out string? path))
                return;

            if (!int.TryParse(point.Attribute("sl")?.Value, out int line))
                return;

            int.TryParse(point.Attribute("sc")?.Value, out int column);
            int.TryParse(point.Attribute("vc")?.Value, out int visits);
            var key = NormalizePath(path);
            if (!result.TryGetValue(key, out FileCoverage? coverage))
            {
                coverage = new FileCoverage();
                result[key] = coverage;
            }

            coverage.Add(line, column, visits > 0);
        }

        public static string NormalizePath(string path)
            => Path.GetFullPath(path).Replace('\\', '/');
    }
}
