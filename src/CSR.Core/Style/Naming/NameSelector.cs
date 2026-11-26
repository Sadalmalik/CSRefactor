using CSR.Core.Checks;

namespace CSR.Core.Style.Naming
{
    // Какие символы задевает правило: все заданные условия вместе; пустое условие - любое значение.
    // Исключения выражает порядок правил (каскад), отрицаний нет
    public class NameSelector
    {
        public static readonly IReadOnlyList<string> Fields = ["kinds", "access", "modifiers", "types", "attributes", "bases"];

        // Модификаторы, которые видит селектор: опечатка в значении - ошибка конфига
        private static readonly IReadOnlyList<string> ModifierWords =
        [
            "static",
            "readonly",
            "const",
            "abstract",
            "virtual",
            "sealed",
            "partial",
            "async",
            "new",
            "extern",
            "unsafe",
            "volatile",
            "required",
        ];

        private IReadOnlyList<string> m_Kinds = Array.Empty<string>();
        private IReadOnlyList<string> m_Access = Array.Empty<string>();
        private IReadOnlyList<string> m_Modifiers = Array.Empty<string>();
        private NameMask m_Types = NameMask.Get([]);
        private NameMask m_Attributes = NameMask.Get([]);
        private NameMask m_Bases = NameMask.Get([]);

        // Виды правила; пусто - любой вид
        public IReadOnlyList<string> Kinds
            => m_Kinds;

        // kinds, access, modifiers - значения; types, attributes, bases - маски (* - любые символы)
        public static NameSelector Parse(Record? record)
        {
            var selector = new NameSelector();
            if (record == null)
                return selector;

            record.RequireKnown(Fields);
            selector.m_Kinds = record.GetStrings("kinds", NameSymbolTool.Kinds);
            selector.m_Access = record.GetStrings("access", NameSymbolTool.Access);
            selector.m_Modifiers = record.GetStrings("modifiers", ModifierWords);
            selector.m_Types = NameMask.Get(record.GetStrings("types"));
            selector.m_Attributes = NameMask.Get(record.GetStrings("attributes"));
            selector.m_Bases = NameMask.Get(record.GetStrings("bases"));
            return selector;
        }

        public bool IsMatch(NameSymbol symbol)
        {
            if (m_Kinds.Count > 0 && !m_Kinds.Contains(symbol.Kind))
                return false;

            if (m_Access.Count > 0 && (symbol.Access == null || !m_Access.Contains(symbol.Access)))
                return false;

            if (!m_Modifiers.All(symbol.Modifiers.Contains))
                return false;

            if (!m_Types.IsEmpty && (symbol.TypeName == null || !m_Types.IsMatch(symbol.TypeName)))
                return false;

            if (!m_Attributes.IsEmpty && !symbol.Attributes.Any(m_Attributes.IsMatch))
                return false;

            return m_Bases.IsEmpty || symbol.Bases.Any(m_Bases.IsMatch);
        }
    }
}
