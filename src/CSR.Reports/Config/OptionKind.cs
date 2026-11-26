using System.Globalization;
using System.Text.Json;

namespace CSR.Reports.Config
{
    // Вид значения для ключа .jsonc и опции командной строки; ошибка - ConfigException с путем ключа или именем опции
    public class OptionKind<TValue>
    {
        public required Func<JsonElement, string, TValue> ReadJson { get; init; }

        // Текст опции командной строки и ее имя; null - только в конфиге
        public Func<string, string, TValue>? ParseText { get; init; }
    }

    public static class OptionKind
    {
        private const string ShareText = "число от 0 до 1";

        // Целое любого знака: параметры проверок и пороги метрик
        public static readonly OptionKind<int> Integer = CreateInt(int.MinValue, "целое число");

        public static readonly OptionKind<int> Count = CreateInt(0, "неотрицательное целое");

        // Доля от 0 до 1; в командной строке - с точкой или запятой
        public static readonly OptionKind<double> Share = new()
        {
            ReadJson = (element, path) => Require(ReadDouble(element), IsShare, path, ShareText),
            ParseText = (text, name) => Require(ParseDouble(text), IsShare, name, ShareText, text),
        };

        public static readonly OptionKind<double> Weight = new()
        {
            ReadJson = (element, path) => Require(ReadDouble(element), value => value >= 0, path, "неотрицательное число"),
        };

        public static readonly OptionKind<string> Text = new()
        {
            ReadJson = (element, path)
                => element.ValueKind == JsonValueKind.String ? element.GetString()! : throw CreateError(path, "строка"),
        };

        public static readonly OptionKind<List<string>> Strings = new() { ReadJson = JsonConfig.ReadStrings };

        // Объект: имя -> массив строк
        public static readonly OptionKind<Dictionary<string, List<string>>> StringLists = new() { ReadJson = ReadStringLists };

        // text - значение опции командной строки (в конфиге - null)
        private static ConfigException CreateError(string name, string expected, string? text = null)
        {
            var message = $"{name}: ожидается {expected}";
            return new ConfigException(text == null ? message : $"{message}, задано '{text}'");
        }

        private static OptionKind<int> CreateInt(int min, string expected)
        {
            return new OptionKind<int>
            {
                ReadJson = (element, path) => Require(ReadInt(element), value => value >= min, path, expected),
                ParseText = (text, name) => Require(ParseInt(text), value => value >= min, name, expected, text),
            };
        }

        private static Dictionary<string, List<string>> ReadStringLists(JsonElement element, string path)
        {
            JsonConfig.RequireObject(element, path);
            var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
                result[property.Name] = JsonConfig.ReadStrings(property.Value, $"{path}.{property.Name}");
            return result;
        }

        // null - значение не того вида
        private static T Require<T>(
            T? value,
            Func<T, bool> isValid,
            string name,
            string expected,
            string? text = null)
            where T : struct
        {
            if (value == null || !isValid(value.Value))
                throw CreateError(name, expected, text);

            return value.Value;
        }

        private static int? ReadInt(JsonElement element)
            => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int value) ? value : null;

        private static double? ReadDouble(JsonElement element)
            => element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double value) ? value : null;

        private static int? ParseInt(string text)
            => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : null;

        private static double? ParseDouble(string text)
        {
            var normalized = text.Replace(',', '.');
            bool isNumber = double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double value);
            return isNumber ? value : null;
        }

        private static bool IsShare(double value)
            => value is >= 0 and <= 1;
    }
}
