using CSR.Core.Arch;
using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Вложенный цикл по коллекции, не зависящей от внешнего цикла: перебор пар, O(N²). Внутренний цикл - foreach
    // или for с условием по Count / Length; не находка, если коллекция внутреннего цикла использует имя, объявленное
    // во внешнем цикле (обход иерархии: строка -> ячейки строки), и коллекции из ignoreCollections. Внешний цикл -
    // foreach или for (while и do - обход очереди, не перебор пар); граница - лямбда и локальная функция
    public class NestedLoopCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("sizeMembers", "члены размера коллекции в условии for", "Count", "Length"),
            CheckParameter.Strings(
                "ignoreCollections",
                "маски коллекций внутреннего цикла (текст выражения без пробелов), которые не находка: короткие постоянные"),
            FileScope.ExcludePaths,
        ];

        public override string Name
            => "nested-loop";

        public override string Description
            => "вложенный цикл по независимой коллекции";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            if (FileScope.IsExcluded(context))
                return;

            var sizeMembers = context.Settings.GetStrings("sizeMembers");
            var ignored = NameMask.Get(context.Settings.GetStrings("ignoreCollections"));
            foreach (var inner in context.Root.DescendantNodes().OfType<StatementSyntax>())
            {
                var collection = GetCollection(inner, sizeMembers);
                if (collection == null
                    || ignored.IsMatch(string.Concat(collection.ToString().Where(symbol => !char.IsWhiteSpace(symbol)))))
                    continue;

                var outer = GetOuterLoop(inner);
                if (outer == null)
                    continue;

                var outerNames = LocalNames.GetDeclared(outer);
                outerNames.ExceptWith(LocalNames.GetDeclared(inner));
                if (LocalNames.GetUsed(collection).Any(outerNames.Contains))
                    continue;

                var message = $"вложенный цикл по '{collection}' внутри цикла {Describe(outer)}: O(N²)";
                context.Report(inner.GetFirstToken().Span, message);
            }
        }

        // Коллекция цикла: foreach - выражение, for - x в условии i < x.Count; null - не цикл по коллекции
        private static ExpressionSyntax? GetCollection(StatementSyntax statement, IReadOnlyList<string> sizeMembers)
        {
            if (statement is CommonForEachStatementSyntax loop)
                return loop.Expression;

            if (statement is not ForStatementSyntax { Condition: BinaryExpressionSyntax condition })
                return null;

            var size = condition.Right as MemberAccessExpressionSyntax ?? condition.Left as MemberAccessExpressionSyntax;
            return size != null && sizeMembers.Contains(size.Name.Identifier.Text) ? size.Expression : null;
        }

        private static StatementSyntax? GetOuterLoop(StatementSyntax inner)
        {
            foreach (var ancestor in inner.Ancestors())
            {
                if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or MemberDeclarationSyntax)
                    return null;

                if (ancestor is CommonForEachStatementSyntax or ForStatementSyntax)
                    return (StatementSyntax)ancestor;
            }

            return null;
        }

        private static string Describe(StatementSyntax loop)
            => loop is CommonForEachStatementSyntax each ? $"по '{each.Expression}'" : "for";
    }
}
