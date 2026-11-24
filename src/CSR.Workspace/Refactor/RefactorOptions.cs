using System.Text.Json;
using CSR.Reports.Config;

namespace CSR.Workspace.Refactor
{
    // Параметры rename из refactor.jsonc
    public class RefactorOptions
    {
        // Атрибуты, при которых имя члена сериализуется (сверх встроенных Unity, Newtonsoft.Json, DataMember):
        // переименование - отказ
        public List<string> SerializationAttributes { get; set; } = [];

        // Расширения файлов, где ищутся строковые совпадения имени (кроме .cs - там только литералы)
        public List<string> StringExtensions { get; set; } = [".lua", ".json", ".yaml", ".yml", ".xml"];

        // Ассеты Unity (YAML): просматриваются, только если имя может быть в сериализованных данных -
        // сериализуемое поле, тип UnityEngine.Object или [Serializable], публичный метод компонента (UnityEvent).
        // На amb это гигабайты: просмотр всех ассетов на каждое переименование - около 9 с
        public List<string> UnityExtensions { get; set; } = [".prefab", ".asset", ".unity"];

        // Папки поиска относительно корня решения; пусто - корень решения
        public List<string> StringRoots { get; set; } = [];

        // Имена папок, которые пропускаются при поиске
        public List<string> StringExclude { get; set; } =
            ["Library", "Temp", "Logs", "obj", "bin", ".git", ".vs", ".idea", "node_modules"];

        public int MaxFileSizeKb { get; set; } = 20480;

        public int MaxStringMatches { get; set; } = 200;
    }

    // refactor.jsonc: { "serialization": { "attributes": [...] },
    //   "strings": { "extensions": [...], "unityExtensions": [...], "roots": [...], "exclude": [...],
    //     "maxFileSizeKb": N, "maxMatches": N } }
    public static class RefactorConfig
    {
        public static RefactorOptions Parse(string json)
        {
            var options = new RefactorOptions();
            using var document = JsonConfig.Parse(json);
            var root = document.RootElement;
            JsonConfig.RequireObject(root, "refactor");
            foreach (var property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "serialization":
                        ReadSerialization(property.Value, options);
                        break;
                    case "strings":
                        ReadStrings(property.Value, options);
                        break;
                    default:
                        throw new ConfigException($"refactor: неизвестный ключ '{property.Name}'");
                }
            }

            return options;
        }

        private static void ReadSerialization(JsonElement element, RefactorOptions options)
        {
            JsonConfig.RequireObject(element, "serialization");
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name != "attributes")
                    throw new ConfigException($"serialization: неизвестный ключ '{property.Name}'");

                options.SerializationAttributes = JsonConfig.ReadStrings(property.Value, "serialization.attributes");
            }
        }

        private static void ReadStrings(JsonElement element, RefactorOptions options)
        {
            JsonConfig.RequireObject(element, "strings");
            foreach (var property in element.EnumerateObject())
            {
                var path = $"strings.{property.Name}";
                switch (property.Name)
                {
                    case "extensions":
                        options.StringExtensions = JsonConfig.ReadStrings(property.Value, path);
                        break;
                    case "unityExtensions":
                        options.UnityExtensions = JsonConfig.ReadStrings(property.Value, path);
                        break;
                    case "roots":
                        options.StringRoots = JsonConfig.ReadStrings(property.Value, path);
                        break;
                    case "exclude":
                        options.StringExclude = JsonConfig.ReadStrings(property.Value, path);
                        break;
                    case "maxFileSizeKb":
                        options.MaxFileSizeKb = JsonConfig.ReadInt(property.Value, path);
                        break;
                    case "maxMatches":
                        options.MaxStringMatches = JsonConfig.ReadInt(property.Value, path);
                        break;
                    default:
                        throw new ConfigException($"strings: неизвестный ключ '{property.Name}'");
                }
            }
        }
    }
}
