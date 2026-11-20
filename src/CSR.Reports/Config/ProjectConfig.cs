using System.Text.Json;
using CSR.Core.Config;

namespace CSR.Reports.Config
{
    // project.jsonc: { "preprocessorSymbols": ["UNITY_EDITOR"] }
    public static class ProjectConfig
    {
        public static ProjectOptions Parse(string json)
        {
            using JsonDocument document = JsonConfig.Parse(json);
            JsonElement root = document.RootElement;
            JsonConfig.RequireObject(root, "project");

            IReadOnlyList<string>? symbols = null;
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (property.Name == "preprocessorSymbols")
                    symbols = JsonConfig.ReadStrings(property.Value, "preprocessorSymbols");
                else
                    throw new ConfigException($"неизвестный ключ '{property.Name}'");
            }

            return symbols == null ? new ProjectOptions() : new ProjectOptions { PreprocessorSymbols = symbols };
        }
    }
}
