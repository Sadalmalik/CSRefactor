using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Объявление переменной в out-аргументе: out var x или out T x; непрочитанная переменная -> out _
    public class OutVarCheck : StyleCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Choice("prefer", "explicit - out T x, var - out var x", "explicit", "var"),
            CheckParameter.Bool("discardUnused", true,
                "переменная, которую дальше по коду функции не читают, - находка с заменой на out _"),
        ];

        public override string Name
            => "out-var";

        public override string Description
            => "тип переменной в out-аргументе";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            bool preferExplicit = context.Settings.GetString("prefer") == "explicit";
            bool discardUnused = context.Settings.GetBool("discardUnused");
            foreach (var argument in context.Root.DescendantNodes().OfType<ArgumentSyntax>())
            {
                if (!argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))
                    continue;

                if (argument.Expression is not DeclarationExpressionSyntax declaration)
                    continue;

                if (declaration.Designation is DiscardDesignationSyntax)
                {
                    var discard = context.ReplaceInLines(declaration.Span, "_");
                    context.Report(declaration.Span, $"'out {declaration}' -> 'out _'", discard);
                    continue;
                }

                if (declaration.Designation is not SingleVariableDesignationSyntax variable)
                    continue;

                if (discardUnused && !IsReadLater(variable))
                {
                    var discard = context.ReplaceInLines(declaration.Span, "_");
                    context.Report(declaration.Span, $"'{variable.Identifier.Text}' дальше не читается -> 'out _'",
                        discard);
                    continue;
                }

                if (preferExplicit && declaration.Type.IsVar)
                {
                    context.Report(declaration.Span, $"'out var {variable.Identifier.Text}' - тип явно");
                    continue;
                }

                if (!preferExplicit && !declaration.Type.IsVar)
                {
                    var replacement = context.ReplaceInLines(declaration.Type.Span, "var");
                    context.Report(declaration.Span, $"'out {declaration.Type}' -> 'out var'", replacement);
                }
            }
        }

        private static bool IsReadLater(SingleVariableDesignationSyntax variable)
        {
            var scope = variable.Ancestors().FirstOrDefault(IsScope);
            if (scope == null)
                return true;

            var name = variable.Identifier.Text;
            return scope.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Any(identifier => identifier.Identifier.Text == name && identifier.SpanStart > variable.SpanStart);
        }

        private static bool IsScope(SyntaxNode node)
        {
            return node is BaseMethodDeclarationSyntax
                or AccessorDeclarationSyntax
                or LocalFunctionStatementSyntax
                or AnonymousFunctionExpressionSyntax
                or BasePropertyDeclarationSyntax
                or FieldDeclarationSyntax;
        }
    }
}
