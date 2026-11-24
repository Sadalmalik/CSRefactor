using System.Globalization;
using System.Text.Json;

namespace CSR.Reports.Config
{
    // Вид значения опции: одно правило для ключа .jsonc и опции командной строки. Ошибка - ConfigException
    // с путем ключа или именем опции
    public class OptionKind<TValue>
    {
        public required Func<JsonElement, string, TValue> ReadJson { get; init; }

        // Текст опции командной строки и ее имя; null - только в конфиге
        public Func<string, string, TValue>? ParseText { get; init; }
    }

    public static class OptionKinds
    {
        private const string ShareText = "число от 0 до 1";

        public static readonly OptionKind<int> Count = Int(0, "неотрицательное целое");

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
                => element.ValueKind == JsonValueKind.String ? element.GetString()! : throw Expected(path, "строка"),
        };

        public static readonly OptionKind<List<string>> Strings = new() { ReadJson = JsonConfig.ReadStrings };

        // text - значение опции командной строки (в конфиге - null)
        private static ConfigException Expected(string name, string expected, string? text = null)
        {
            var message = $"{name}: ожидается {expected}";
            return new ConfigException(text == null ? message : $"{message}, задано '{text}'");
        }

        private static OptionKind<int> Int(int min, string expected)
        {
            return new OptionKind<int>
            {
                ReadJson = (element, path) => Require(ReadInt(element), value => value >= min, path, expected),
                ParseText = (text, name) => Require(ParseInt(text), value => value >= min, name, expected, text),
            };
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
                throw Expected(name, expected, text);

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
