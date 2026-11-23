using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Legacy
{
    // Перевод старых проверок именования (до v0.4): записи конфига naming, разбор - тот же, что у конфига
    public static class LegacyRules
    {
        public const string Replacement = "naming (правила - DOCS.md, раздел naming, таблица перевода старых проверок)";

        // Анализ файла правилами перевода
        public static Action<CheckContext> Analyze(IReadOnlyList<NamingRule> rules)
            => context => NamingCheck.Run(context, rules);

        public static NamingRule Rule(Dictionary<string, object> fields)
            => NamingRule.Parse(new Record(fields));

        public static Record Symbols(Dictionary<string, object> fields)
            => new(fields, "symbols");

        // Виды из параметра members, которые знает naming: неизвестный вид старая проверка молча пропускала
        public static IReadOnlyList<string> KnownKinds(IReadOnlyList<string> kinds)
            => kinds.Where(NameSymbols.Kinds.Contains).ToList();
    }
}
