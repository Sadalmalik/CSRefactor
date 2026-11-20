using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Config
{
    // Общие сведения о проекте (project.jsonc), на которые ссылаются режимы metrics, style и arch
    public class ProjectOptions
    {
        public IReadOnlyList<string> PreprocessorSymbols { get; init; } = ["UNITY_EDITOR"];

        public CSharpParseOptions ParseOptions
            => new(LanguageVersion.Latest, DocumentationMode.Parse, SourceCodeKind.Regular, PreprocessorSymbols);

        public static ProjectOptions FromJson(string json)
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
