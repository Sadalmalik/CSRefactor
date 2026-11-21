using System.Text.Json;
using CSR.Core.Config;

namespace CSR.Reports.Config
{
    // project.jsonc: { "preprocessorSymbols": ["UNITY_EDITOR"],
    //   "logger": { "methods": ["Debug.Log", "*.Logger.Error"], "bypass": ["Console.Write*"] },
    //   "mechanisms": { "pool": { "use": "ObjectPool<T>", "fields": ["Stack<*>"], "methods": ["Rent*", "Return*"] } } }.
    // logger.bypass - вызовы в обход логгера проекта: механизм "logger" с заменой на logger.methods
    public static class ProjectConfig
    {
        private const string LoggerMechanism = "logger";

        public static ProjectOptions Parse(string json)
        {
            using var document = JsonConfig.Parse(json);
            var root = document.RootElement;
            JsonConfig.RequireObject(root, "project");

            IReadOnlyList<string>? symbols = null;
            var logger = new LoggerSection();
            var mechanisms = new List<ProjectMechanism>();
            foreach (var property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "preprocessorSymbols":
                        symbols = JsonConfig.ReadStrings(property.Value, "preprocessorSymbols");
                        break;
                    case "logger":
                        logger = ReadLogger(property.Value);
                        break;
                    case "mechanisms":
                        mechanisms.AddRange(ReadMechanisms(property.Value));
                        break;
                    default:
                        throw new ConfigException($"неизвестный ключ '{property.Name}'");
                }
            }

            if (logger.Bypass.Count > 0)
                mechanisms.Add(CreateLoggerMechanism(logger, mechanisms));

            if (symbols == null)
                return new ProjectOptions { LoggerMethods = logger.Methods, Mechanisms = mechanisms };

            return new ProjectOptions
            {
                PreprocessorSymbols = symbols,
                LoggerMethods = logger.Methods,
                Mechanisms = mechanisms,
            };
        }

        private static LoggerSection ReadLogger(JsonElement element)
        {
            JsonConfig.RequireObject(element, "logger");
            var logger = new LoggerSection();
            foreach (var property in element.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "methods":
                        logger.Methods = JsonConfig.ReadStrings(property.Value, "logger.methods");
                        break;
                    case "bypass":
                        logger.Bypass = JsonConfig.ReadStrings(property.Value, "logger.bypass");
                        break;
                    default:
                        throw new ConfigException($"logger: неизвестный ключ '{property.Name}'");
                }
            }

            return logger;
        }

        private static List<ProjectMechanism> ReadMechanisms(JsonElement element)
        {
            JsonConfig.RequireObject(element, "mechanisms");
            var mechanisms = new List<ProjectMechanism>();
            foreach (var property in element.EnumerateObject())
                mechanisms.Add(ReadMechanism(property.Name, property.Value));

            return mechanisms;
        }

        private static ProjectMechanism ReadMechanism(string name, JsonElement element)
        {
            var path = "mechanisms." + name;
            JsonConfig.RequireObject(element, path);
            string? use = null;
            var lists = new Dictionary<string, List<string>>();
            foreach (var property in element.EnumerateObject())
            {
                var key = path + "." + property.Name;
                switch (property.Name)
                {
                    case "use":
                        use = ReadString(property.Value, key);
                        break;
                    case "types" or "fields" or "methods" or "calls" or "creates" or "excludePaths":
                        lists[property.Name] = JsonConfig.ReadStrings(property.Value, key);
                        break;
                    default:
                        throw new ConfigException($"{path}: неизвестный ключ '{property.Name}'");
                }
            }

            if (use == null)
                throw new ConfigException($"{path}: не задан use - чем заменить повтор");

            if (!lists.Keys.Any(key => key != "excludePaths"))
            {
                const string keys = "types, fields, methods, calls, creates";
                throw new ConfigException($"{path}: не задано, как распознать повтор ({keys})");
            }

            return new ProjectMechanism
            {
                Name = name,
                Use = use,
                Types = GetList(lists, "types"),
                Fields = GetList(lists, "fields"),
                Methods = GetList(lists, "methods"),
                Calls = GetList(lists, "calls"),
                Creates = GetList(lists, "creates"),
                ExcludePaths = GetList(lists, "excludePaths"),
            };
        }

        private static ProjectMechanism CreateLoggerMechanism(LoggerSection logger, List<ProjectMechanism> mechanisms)
        {
            if (mechanisms.Any(mechanism => mechanism.Name == LoggerMechanism))
                throw new ConfigException("logger.bypass и mechanisms.logger заданы оба: оставьте один");

            if (logger.Methods.Count == 0)
                throw new ConfigException("logger.bypass без logger.methods: нечем заменить вызовы");

            var use = string.Join(", ", logger.Methods);
            return new ProjectMechanism { Name = LoggerMechanism, Use = use, Calls = logger.Bypass };
        }

        private static IReadOnlyList<string> GetList(Dictionary<string, List<string>> lists, string key)
            => lists.TryGetValue(key, out List<string>? values) ? values : Array.Empty<string>();

        private static string ReadString(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.String)
                throw new ConfigException($"{path}: ожидается строка");

            return value.GetString()!;
        }

        // Раздел logger: методы логгера проекта и вызовы в обход него
        private class LoggerSection
        {
            public IReadOnlyList<string> Methods { get; set; } = Array.Empty<string>();

            public IReadOnlyList<string> Bypass { get; set; } = Array.Empty<string>();
        }
    }
}
