using System.Text.Json;
using CSR.Core.Config;

namespace CSR.Reports.Config
{
    // project.jsonc: { "preprocessorSymbols": ["UNITY_EDITOR"], "logger": { "methods": ["Debug.Log", "*.Logger.Error"] } }
    public static class ProjectConfig
    {
        public static ProjectOptions Parse(string json)
        {
            using var document = JsonConfig.Parse(json);
            var root = document.RootElement;
            JsonConfig.RequireObject(root, "project");

            IReadOnlyList<string>? symbols = null;
            IReadOnlyList<string> loggerMethods = Array.Empty<string>();
            foreach (var property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "preprocessorSymbols":
                        symbols = JsonConfig.ReadStrings(property.Value, "preprocessorSymbols");
                        break;
                    case "logger":
                        loggerMethods = ReadLogger(property.Value);
                        break;
                    default:
                        throw new ConfigException($"неизвестный ключ '{property.Name}'");
                }
            }

            return symbols == null
                ? new ProjectOptions { LoggerMethods = loggerMethods }
                : new ProjectOptions { PreprocessorSymbols = symbols, LoggerMethods = loggerMethods };
        }

        private static IReadOnlyList<string> ReadLogger(JsonElement element)
        {
            JsonConfig.RequireObject(element, "logger");
            IReadOnlyList<string> methods = Array.Empty<string>();
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name != "methods")
                    throw new ConfigException($"logger: неизвестный ключ '{property.Name}'");

                methods = JsonConfig.ReadStrings(property.Value, "logger.methods");
            }

            return methods;
        }
    }
}
