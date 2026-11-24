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

        private static readonly OptionTable<LoggerSection> Logger = new OptionTable<LoggerSection>()
            .Add("methods", OptionKinds.Strings, (logger, value) => logger.Methods = value)
            .Add("bypass", OptionKinds.Strings, (logger, value) => logger.Bypass = value);

        private static readonly OptionTable<MechanismSection> Mechanism = new OptionTable<MechanismSection>()
            .Add("use", OptionKinds.Text, (mechanism, value) => mechanism.Use = value)
            .Add("types", OptionKinds.Strings, (mechanism, value) => mechanism.Types = value)
            .Add("fields", OptionKinds.Strings, (mechanism, value) => mechanism.Fields = value)
            .Add("methods", OptionKinds.Strings, (mechanism, value) => mechanism.Methods = value)
            .Add("calls", OptionKinds.Strings, (mechanism, value) => mechanism.Calls = value)
            .Add("creates", OptionKinds.Strings, (mechanism, value) => mechanism.Creates = value)
            .Add("excludePaths", OptionKinds.Strings, (mechanism, value) => mechanism.ExcludePaths = value);

        private static readonly OptionKind<List<ProjectMechanism>> Mechanisms = new() { ReadJson = ReadMechanisms };

        private static readonly OptionTable<ProjectSection> Table = new OptionTable<ProjectSection>()
            .Add("preprocessorSymbols", OptionKinds.Strings, (project, value) => project.Symbols = value)
            .AddSection("logger", Logger, project => project.Logger)
            .Add("mechanisms", Mechanisms, (project, value) => project.Mechanisms.AddRange(value));

        public static ProjectOptions Parse(string json)
        {
            var project = new ProjectSection();
            Table.ReadRoot(json, "project", project);
            var logger = project.Logger;
            var mechanisms = project.Mechanisms;
            if (logger.Bypass.Count > 0)
                mechanisms.Add(CreateLoggerMechanism(logger, mechanisms));

            if (project.Symbols == null)
                return new ProjectOptions { LoggerMethods = logger.Methods, Mechanisms = mechanisms };

            return new ProjectOptions
            {
                PreprocessorSymbols = project.Symbols,
                LoggerMethods = logger.Methods,
                Mechanisms = mechanisms,
            };
        }

        private static List<ProjectMechanism> ReadMechanisms(JsonElement element, string path)
        {
            JsonConfig.RequireObject(element, path);
            var mechanisms = new List<ProjectMechanism>();
            foreach (var property in element.EnumerateObject())
            {
                var section = new MechanismSection();
                var mechanismPath = $"{path}.{property.Name}";
                Mechanism.Read(property.Value, mechanismPath, section);
                mechanisms.Add(section.Create(property.Name, mechanismPath));
            }

            return mechanisms;
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

        // Раздел logger: методы логгера проекта и вызовы в обход него
        private class LoggerSection
        {
            public IReadOnlyList<string> Methods { get; set; } = Array.Empty<string>();

            public IReadOnlyList<string> Bypass { get; set; } = Array.Empty<string>();
        }

        private class ProjectSection
        {
            public IReadOnlyList<string>? Symbols { get; set; }

            public LoggerSection Logger { get; } = new();

            public List<ProjectMechanism> Mechanisms { get; } = new();
        }

        // Раздел механизма; списки null - не заданы
        private class MechanismSection
        {
            public string? Use { get; set; }

            public IReadOnlyList<string>? Types { get; set; }

            public IReadOnlyList<string>? Fields { get; set; }

            public IReadOnlyList<string>? Methods { get; set; }

            public IReadOnlyList<string>? Calls { get; set; }

            public IReadOnlyList<string>? Creates { get; set; }

            public IReadOnlyList<string>? ExcludePaths { get; set; }

            // Механизм с проверкой: use задан и есть хотя бы один признак повтора
            public ProjectMechanism Create(string name, string path)
            {
                if (Use == null)
                    throw new ConfigException($"{path}: не задан use - чем заменить повтор");

                if (Types == null && Fields == null && Methods == null && Calls == null && Creates == null)
                {
                    const string keys = "types, fields, methods, calls, creates";
                    throw new ConfigException($"{path}: не задано, как распознать повтор ({keys})");
                }

                return new ProjectMechanism
                {
                    Name = name,
                    Use = Use,
                    Types = Types ?? Array.Empty<string>(),
                    Fields = Fields ?? Array.Empty<string>(),
                    Methods = Methods ?? Array.Empty<string>(),
                    Calls = Calls ?? Array.Empty<string>(),
                    Creates = Creates ?? Array.Empty<string>(),
                    ExcludePaths = ExcludePaths ?? Array.Empty<string>(),
                };
            }
        }
    }
}
