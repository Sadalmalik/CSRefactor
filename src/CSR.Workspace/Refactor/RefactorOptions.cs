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
        private static readonly OptionTable<RefactorOptions> Serialization = new OptionTable<RefactorOptions>()
            .Add("attributes", OptionKind.Strings, (options, value) => options.SerializationAttributes = value);

        private static readonly OptionTable<RefactorOptions> Strings = new OptionTable<RefactorOptions>()
            .Add("extensions", OptionKind.Strings, (options, value) => options.StringExtensions = value)
            .Add("unityExtensions", OptionKind.Strings, (options, value) => options.UnityExtensions = value)
            .Add("roots", OptionKind.Strings, (options, value) => options.StringRoots = value)
            .Add("exclude", OptionKind.Strings, (options, value) => options.StringExclude = value)
            .Add("maxFileSizeKb", OptionKind.Count, (options, value) => options.MaxFileSizeKb = value)
            .Add("maxMatches", OptionKind.Count, (options, value) => options.MaxStringMatches = value);

        private static readonly OptionTable<RefactorOptions> Table = new OptionTable<RefactorOptions>()
            .AddSection("serialization", Serialization, options => options)
            .AddSection("strings", Strings, options => options);

        public static RefactorOptions Parse(string json)
        {
            var options = new RefactorOptions();
            Table.ReadRoot(json, "refactor", options, "refactor");
            return options;
        }
    }
}
