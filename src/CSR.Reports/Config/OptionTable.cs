using System.Text.Json;

namespace CSR.Reports.Config
{
    // Ключи раздела .jsonc: ключ -> вид значения -> присвоение; ключ с опцией читается и из командной строки
    public class OptionTable<T>
    {
        private readonly Dictionary<string, Action<T, JsonElement, string>> m_Keys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Action<T, string>> m_Options = new(StringComparer.Ordinal);

        // Опции командной строки таблицы - в набор допустимых опций команды
        public IEnumerable<string> OptionNames
            => m_Options.Keys;

        public OptionTable<T> Add<TValue>(
            string key,
            OptionKind<TValue> kind,
            Action<T, TValue> set,
            string? option = null)
        {
            m_Keys.Add(key, (target, element, path) => set(target, kind.ReadJson(element, path)));
            if (option == null)
                return this;

            var parse = kind.ParseText ?? throw new ArgumentException($"у вида ключа '{key}' нет чтения из текста");
            m_Options.Add(option, (target, text) => set(target, parse(text, option)));
            return this;
        }

        // Раздел - ключи вложенного объекта опций; options => options - ключи в том же объекте
        public OptionTable<T> AddSection<TSection>(string key, OptionTable<TSection> table, Func<T, TSection> section)
        {
            m_Keys.Add(key, (target, element, path) => table.Read(element, path, section(target)));
            return this;
        }

        // Корень документа; name - имя конфига в ошибке "не объект", unknownPrefix - префикс ошибки неизвестного ключа
        public void ReadRoot(string json, string name, T target, string unknownPrefix = "")
        {
            using var document = JsonConfig.Parse(json);
            JsonConfig.RequireObject(document.RootElement, name);
            ReadKeys(document.RootElement, "", unknownPrefix, target);
        }

        // path - путь объекта в ошибках: ключи - path.key
        public void Read(JsonElement element, string path, T target)
        {
            JsonConfig.RequireObject(element, path);
            ReadKeys(element, path, path, target);
        }

        // Опции командной строки таблицы: getOption - значение опции или null
        public void ReadOptions(Func<string, string?> getOption, T target)
        {
            foreach (var pair in m_Options)
            {
                var text = getOption(pair.Key);
                if (text != null)
                    pair.Value(target, text);
            }
        }

        private void ReadKeys(JsonElement element, string path, string unknownPrefix, T target)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!m_Keys.TryGetValue(property.Name, out Action<T, JsonElement, string>? read))
                {
                    var message = $"неизвестный ключ '{property.Name}'";
                    throw new ConfigException(unknownPrefix.Length == 0 ? message : $"{unknownPrefix}: {message}");
                }

                read(target, property.Value, path.Length == 0 ? property.Name : $"{path}.{property.Name}");
            }
        }
    }
}
