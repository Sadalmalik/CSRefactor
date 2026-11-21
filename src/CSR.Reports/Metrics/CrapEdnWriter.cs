using System.Globalization;
using System.Text;
using CSR.Core.Metrics;

namespace CSR.Reports.Metrics
{
    // Снимок .metrics/crap.edn в формате crapper (crap4clj): {:entries [{:name :namespace :complexity :coverage :crap}]}.
    // :namespace - пространство имен и объемлющие типы, :name - член типа (Method, Value.get, Method.Local).
    // Без покрытия :coverage и :crap - nil. Порядок: худший CRAP первым, без оценки - по убыванию сложности
    public static class CrapEdnWriter
    {
        public static string Write(IEnumerable<FileMetrics> files)
        {
            var entries = Sort(files.SelectMany(file => file.Functions)).ToList();
            if (entries.Count == 0)
                return "{:entries []}\n";

            var builder = new StringBuilder("{:entries [\n");
            for (var index = 0; index < entries.Count; index++)
            {
                var function = entries[index];
                builder.Append("  {")
                    .Append(":name ").Append(EdnString(function.Member)).Append(", ")
                    .Append(":namespace ").Append(EdnString(GetNamespace(function))).Append(", ")
                    .Append(":complexity ").Append(function.Values[MetricKeys.Cyclomatic]).Append(", ")
                    .Append(":coverage ").Append(EdnNumber(function.Coverage)).Append(", ")
                    .Append(":crap ").Append(EdnNumber(function.Crap))
                    .Append('}');
                builder.Append(index + 1 < entries.Count ? "\n" : "\n]}\n");
            }

            return builder.ToString();
        }

        public static IEnumerable<MetricItem> Sort(IEnumerable<MetricItem> functions)
        {
            return functions
                .OrderBy(function => function.Crap == null ? 1 : 0)
                .ThenByDescending(function => function.Crap ?? function.Values[MetricKeys.Cyclomatic])
                .ThenBy(GetNamespace, StringComparer.Ordinal)
                .ThenBy(function => function.Member, StringComparer.Ordinal)
                .ThenBy(function => function.File, StringComparer.Ordinal);
        }

        public static string GetNamespace(MetricItem function)
        {
            if (function.Namespace.Length == 0)
                return function.Owner;

            return function.Owner.Length == 0 ? function.Namespace : function.Namespace + "." + function.Owner;
        }

        private static string EdnString(string value)
            => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        // Как в crapper: 4 знака без нулей в конце, у целого - ".0"
        private static string EdnNumber(double? value)
        {
            if (value == null)
                return "nil";

            var text = value.Value.ToString("F4", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
            return text.Contains('.') ? text : text + ".0";
        }
    }
}
