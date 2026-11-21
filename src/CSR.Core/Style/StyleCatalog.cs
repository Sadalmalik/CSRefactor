using CSR.Core.Style.Checks;

namespace CSR.Core.Style
{
    // Все проверки движка; конфиг включает их по имени
    public static class StyleCatalog
    {
        public static readonly IReadOnlyList<StyleCheck> All =
        [
            new LineLengthCheck(),
            new DirectiveColumnCheck(),
            new BinaryOperatorWrapCheck(),
            new XmlDocCheck(),
            new CommentSeparatorCheck(),
            new CommentTypographyCheck(),
            new CommentEndingCheck(),
            new CommentedCodeCheck(),
            new TodoFormatCheck(),
            new LogCallWrapCheck(),
            new MessageTextCheck(),
            new NullCheckCheck(),
            new CoalesceAssignmentCheck(),
            new OutVarCheck(),
            new BoolLocalTypeCheck(),
            new LocalTypeCheck(),
            new TupleTypeCheck(),
            new ExplicitPrivateCheck(),
            new ForbiddenModifierCheck(),
            new CustomDelegateCheck(),
        ];

        public static StyleCheck? Find(string name)
            => All.FirstOrDefault(check => check.Name == name);

        // Конфигурация проверки с параметрами по умолчанию
        public static CheckConfiguration Configure(StyleCheck check, Severity severity, string? rule = null)
        {
            return new CheckConfiguration
            {
                Check = check,
                Severity = severity,
                Rule = rule,
                Settings = new CheckSettings(check.Parameters),
            };
        }
    }
}
