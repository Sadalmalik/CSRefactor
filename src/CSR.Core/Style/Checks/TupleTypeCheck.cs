using CSR.Core.Checks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Кортеж-тип в сигнатуре (результат, параметр), поле, свойство, событие.
    // Кортежи в теле метода, в инициализаторах и деконструкция не проверяются
    public class TupleTypeCheck : SyntaxCheck
    {
        public override string Name
            => "tuple-type";

        public override string Description
            => "кортеж в сигнатуре или поле";

        public override void Analyze(CheckContext context)
        {
            foreach (var tuple in context.Root.DescendantNodes().OfType<TupleTypeSyntax>())
            {
                if (tuple.Ancestors().OfType<TupleTypeSyntax>().Any())
                    continue;

                if (IsInSignature(tuple))
                    context.Report(tuple.Span, $"кортеж '{tuple}' в сигнатуре или поле");
            }
        }

        private static bool IsInSignature(TupleTypeSyntax tuple)
        {
            foreach (var ancestor in tuple.Ancestors())
            {
                if (ancestor is BlockSyntax or ArrowExpressionClauseSyntax or EqualsValueClauseSyntax
                    or AnonymousFunctionExpressionSyntax)
                    return false;

                if (ancestor is MemberDeclarationSyntax or LocalFunctionStatementSyntax)
                    return true;

                if (ancestor is StatementSyntax)
                    return false;
            }

            return false;
        }
    }
}
