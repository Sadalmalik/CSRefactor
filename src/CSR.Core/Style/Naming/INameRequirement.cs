using CSR.Core.Checks;

namespace CSR.Core.Style.Naming
{
    // Требование к имени - точка расширения naming: одна реализация на вид требования; ключ каскада - у вида
    // в NamingCatalog. Check - текст нарушения или null
    public interface INameRequirement
    {
        string? Check(NameSymbol symbol);
    }

    // Вид требования в реестре: ключ каскада, поля записи правила и разбор; Create - null, если полей вида в записи нет
    public class NameRequirementKind
    {
        private readonly Func<Record, INameRequirement> m_Create;

        public NameRequirementKind(string key, IReadOnlyList<string> fields, Func<Record, INameRequirement> create)
        {
            Key = key;
            Fields = fields;
            m_Create = create;
        }

        public string Key { get; }

        public IReadOnlyList<string> Fields { get; }

        public INameRequirement? Create(Record record)
            => Fields.Any(record.Has) ? m_Create(record) : null;
    }
}
