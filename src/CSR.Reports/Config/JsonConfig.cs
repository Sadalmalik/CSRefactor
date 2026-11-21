using System.Text.Json;

namespace CSR.Reports.Config
{
    // Конфиги CSR - JSON с комментариями и запятыми после последнего элемента (.jsonc)
    public static class JsonConfig
    {
        private static readonly JsonDocumentOptions Options = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public static JsonDocument Parse(string json)
            => JsonDocument.Parse(json, Options);

        public static void RequireObject(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new ConfigException($"{path}: ожидается объект");
        }

        public static int ReadInt(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
                throw new ConfigException($"{path}: ожидается целое число");

            return value;
        }

        public static bool ReadBool(JsonElement element, string path)
        {
            if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new ConfigException($"{path}: ожидается true или false");

            return element.GetBoolean();
        }

        public static List<string> ReadStrings(JsonElement element, string path)
        {
            if (element.ValueKind != JsonValueKind.Array)
                throw new ConfigException($"{path}: ожидается массив строк");

            var values = new List<string>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                    throw new ConfigException($"{path}: ожидается массив строк");

                values.Add(item.GetString()!);
            }

            return values;
        }
    }

    public class ConfigException : Exception
    {
        public ConfigException(string message)
            : base(message)
        {
        }
    }
}
