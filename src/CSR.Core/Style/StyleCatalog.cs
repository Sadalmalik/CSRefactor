using CSR.Core.Checks;
using CSR.Core.Style.Checks;

namespace CSR.Core.Style
{
    // Все проверки движка; конфиг включает их по имени
    public static class StyleCatalog
    {
        public static readonly IReadOnlyList<SyntaxCheck> All =
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
            new ExpressionArrowCheck(),
            new SignatureWrapCheck(),
            new CallWrapCheck(),
            new RegionLayoutCheck(),
            new ConditionWrapCheck(),
            new InitializerLayoutCheck(),
            new TernaryWrapCheck(),
            new FieldPrefixCheck(),
            new BoolNameCheck(),
            new EventNameCheck(),
            new TypeNameCheck(),
            new NamespaceNameCheck(),
            new EnumPluralCheck(),
            new CollectionNameCheck(),
            new ForbiddenCallCheck(),
            new ForbiddenTypeCheck(),
            new CoroutineCheck(),
            new StaticStateCheck(),
            new LocalFunctionCheck(),
            new PathConcatCheck(),
            new BoolFieldCountCheck(),
            new TypePerFileCheck(),
            new ClosureInMethodCheck(),
            new NestedLoopCheck(),
        ];

        public static SyntaxCheck? Find(string name)
            => All.FirstOrDefault(check => check.Name == name);
    }
}
