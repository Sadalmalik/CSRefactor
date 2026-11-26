using System.Text.Json;
using CSR.Core.Checks;

namespace CSR.Reports.Config
{
    // Конфиг проверок по каталогу (style.jsonc, arch.jsonc):
    // { "checks": { "line-length": { "severity": "error", "rule": "F20", "max": 120 } } }.
    // Проверка, которой нет в конфиге, выключена. Несколько конфигов накладываются по порядку:
    // следующий меняет уровень, метку и параметры, "enabled": false выключает
    public static class CheckConfig
    {
        public static void Apply(string json, CheckOptions options, IReadOnlyList<SyntaxCheck> catalog)
        {
            using var document = JsonConfig.Parse(json);
            var root = document.RootElement;
            JsonConfig.RequireObject(root, "style");
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name != "checks")
                    throw new ConfigException($"неизвестный ключ '{property.Name}'");

                JsonConfig.RequireObject(property.Value, "checks");
                foreach (var check in property.Value.EnumerateObject())
                    ApplyCheck(options, catalog, check.Name, check.Value);
            }
        }

        private static void ApplyCheck(
            CheckOptions options,
            IReadOnlyList<SyntaxCheck> catalog,
            string name,
            JsonElement element)
        {
            var check = catalog.FirstOrDefault(candidate => candidate.Name == name);
            if (check == null)
                throw new ConfigException($"неизвестная проверка '{name}'");

            JsonConfig.RequireObject(element, name);
            var configuration = options.Checks.FirstOrDefault(candidate => candidate.Check == check);
            if (configuration == null)
            {
                configuration = CheckConfiguration.Create(check, Severity.Warning);
                options.Checks.Add(configuration);
            }

            foreach (var property in element.EnumerateObject())
            {
                var path = name + "." + property.Name;
                switch (property.Name)
                {
                    case "enabled":
                        if (!JsonConfig.ReadBool(property.Value, path))
                            options.Checks.Remove(configuration);
                        break;
                    case "severity":
                        configuration.Severity = ReadSeverity(property.Value, path);
                        break;
                    case "rule":
                        configuration.Rule = ReadString(property.Value, path);
                        break;
                    default:
                        ApplyParameter(check, configuration.Settings, property, path);
                        break;
                }
            }
        }

        private static void ApplyParameter(
            SyntaxCheck check,
            CheckSettings settings,
            JsonProperty property,
            string path)
        {
            var parameter = check.Parameters.FirstOrDefault(candidate => candidate.Name == property.Name);
            if (parameter == null)
                throw new ConfigException($"{check.Name}: неизвестный параметр '{property.Name}'");

            var value = property.Value;
            object parsed = parameter.Kind switch
            {
                ParameterKind.Int => OptionKinds.Integer.ReadJson(value, path),
                ParameterKind.Bool => JsonConfig.ReadBool(value, path),
                ParameterKind.String => ReadChoice(parameter, value, path),
                ParameterKind.Strings => JsonConfig.ReadStrings(value, path),
                ParameterKind.Records => ReadRecords(parameter, value, path),
                _ => ReadMap(value, path),
            };

            settings.Set(parameter.Name, parsed);
        }

        private static string ReadChoice(CheckParameter parameter, JsonElement value, string path)
        {
            var text = ReadString(value, path);
            if (parameter.AllowedValues != null && !parameter.AllowedValues.Contains(text))
                throw new ConfigException($"{path}: допустимо {string.Join(", ", parameter.AllowedValues)}");

            return text;
        }

        private static Dictionary<string, string> ReadMap(JsonElement value, string path)
        {
            JsonConfig.RequireObject(value, path);
            var map = new Dictionary<string, string>();
            foreach (var property in value.EnumerateObject())
                map[property.Name] = ReadString(property.Value, path + "." + property.Name);

            return map;
        }

        // Записи: массив объектов; каждая разбирается проверкой (ParseRecord), ошибка разбора - ошибка конфига с путем
        private static List<object> ReadRecords(CheckParameter parameter, JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Array)
                throw new ConfigException($"{path}: ожидается массив записей");

            var records = new List<object>();
            int index = 0;
            foreach (var item in value.EnumerateArray())
            {
                var itemPath = $"{path}[{index++}]";
                try
                {
                    records.Add(parameter.ParseRecord!(ReadRecord(item, itemPath)));
                }
                catch (FormatException exception)
                {
                    throw new ConfigException($"{itemPath}: {exception.Message}");
                }
            }

            return records;
        }

        // Объект JSON -> запись: строка, bool, целое, список строк, вложенная запись
        // relative - путь записи внутри элемента списка (symbols) для текстов ошибок Record
        private static Record ReadRecord(JsonElement value, string path, string relative = "")
        {
            JsonConfig.RequireObject(value, path);
            var fields = new Dictionary<string, object>();
            foreach (var property in value.EnumerateObject())
            {
                var fieldPath = $"{path}.{property.Name}";
                fields[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString()!,
                    JsonValueKind.True or JsonValueKind.False => property.Value.GetBoolean(),
                    JsonValueKind.Number => OptionKinds.Integer.ReadJson(property.Value, fieldPath),
                    JsonValueKind.Array => JsonConfig.ReadStrings(property.Value, fieldPath),
                    JsonValueKind.Object => ReadRecord(property.Value, fieldPath, property.Name),
                    _ => throw new ConfigException($"{fieldPath}: ожидается строка, число, bool, список строк или объект"),
                };
            }

            return new Record(fields, relative);
        }

        private static Severity ReadSeverity(JsonElement value, string path)
        {
            var severity = Finding.ParseSeverity(ReadString(value, path));
            return severity ?? throw new ConfigException($"{path}: допустимо error, warning, hint");
        }

        private static string ReadString(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.String)
                throw new ConfigException($"{path}: ожидается строка");

            return value.GetString()!;
        }
    }
}
