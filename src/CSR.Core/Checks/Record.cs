namespace CSR.Core.Checks
{
    // Запись конфига (элемент параметра-списка): поля - строка, bool, целое, список строк, вложенная запись.
    // Чтение с проверкой вида; неверное или неизвестное поле - FormatException с путем поля от элемента списка
    // (путь элемента добавляет загрузчик конфига)
    public class Record
    {
        private readonly IReadOnlyDictionary<string, object> m_Fields;
        private readonly string m_Path;

        // path - путь записи внутри элемента списка: "" - сам элемент, "symbols" - вложенная запись
        public Record(IReadOnlyDictionary<string, object> fields, string path = "")
        {
            m_Fields = fields;
            m_Path = path;
        }

        public bool Has(string key)
            => m_Fields.ContainsKey(key);

        public string? GetString(string key)
            => Get<string>(key, "строка");

        public bool? GetBool(string key)
            => m_Fields.TryGetValue(key, out object? value) ? Require<bool>(key, value, "true или false") : null;

        public int? GetInt(string key)
            => m_Fields.TryGetValue(key, out object? value) ? Require<int>(key, value, "целое число") : null;

        public IReadOnlyList<string> GetStrings(string key)
            => Get<IReadOnlyList<string>>(key, "список строк") ?? Array.Empty<string>();

        // Список строк из допустимых значений: опечатка в значении - ошибка, а не правило без совпадений
        public IReadOnlyList<string> GetStrings(string key, IReadOnlyCollection<string> allowed)
        {
            var values = GetStrings(key);
            var unknown = values.FirstOrDefault(value => !allowed.Contains(value));
            if (unknown != null)
                throw new FormatException($"{GetPath(key)}: '{unknown}' - допустимо {string.Join(", ", allowed)}");

            return values;
        }

        public Record? GetRecord(string key)
            => Get<Record>(key, "объект");

        // Строка из допустимых значений; null - поля нет
        public string? GetChoice(string key, params string[] allowed)
        {
            var value = GetString(key);
            if (value != null && !allowed.Contains(value))
                throw new FormatException($"{GetPath(key)}: допустимо {string.Join(", ", allowed)}");

            return value;
        }

        // Поля записи, которых нет в known: опечатка в конфиге - ошибка, а не тихо пропущенное правило
        public void RequireKnown(IEnumerable<string> known)
        {
            var unknown = m_Fields.Keys.Except(known).FirstOrDefault();
            if (unknown == null)
                return;

            var place = m_Path.Length > 0 ? m_Path + ": " : "";
            throw new FormatException($"{place}неизвестное поле '{unknown}'");
        }

        private T? Get<T>(string key, string kind)
            where T : class
        {
            return m_Fields.TryGetValue(key, out object? value) ? Require<T>(key, value, kind) : null;
        }

        private T Require<T>(string key, object value, string kind)
        {
            if (value is T typed)
                return typed;

            throw new FormatException($"{GetPath(key)}: ожидается {kind}");
        }

        private string GetPath(string key)
            => m_Path.Length > 0 ? m_Path + "." + key : key;
    }
}
