using CSR.Core.Checks;

namespace CSR.Core.Style.Naming
{
    // Вид требования в реестре: ключ каскада, главные поля записи правила (хотя бы одно - требование есть) и
    // уточняющие (только вместе с главным), разбор
    public class NameRequirementKind
    {
        private readonly Func<Record, INameRequirement> m_Create;

        public NameRequirementKind(
            string key,
            IReadOnlyList<string> mainFields,
            IReadOnlyList<string> extraFields,
            Func<Record, INameRequirement> create)
        {
            Key = key;
            MainFields = mainFields;
            ExtraFields = extraFields;
            m_Create = create;
        }

        public string Key { get; }

        public IReadOnlyList<string> MainFields { get; }

        public IReadOnlyList<string> ExtraFields { get; }

        public IEnumerable<string> Fields
            => MainFields.Concat(ExtraFields);

        // Требование записи; null - полей вида нет. Уточняющее поле без главного - ошибка: оно ничего не проверяет
        public INameRequirement? Create(Record record)
        {
            if (MainFields.Any(record.Has))
                return m_Create(record);

            var extra = ExtraFields.FirstOrDefault(record.Has);
            if (extra != null)
                throw new FormatException($"{extra}: нужно одно из {string.Join(", ", MainFields)}");

            return null;
        }
    }
}
