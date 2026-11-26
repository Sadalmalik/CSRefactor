using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // [Obsolete] без причины: без аргументов или с пустой строкой
    public class ObsoleteReasonCheck : ArchCheck
    {
        public override string Name
            => "obsolete-reason";

        public override string Description
            => "[Obsolete] без причины";

        public override string Question
            => "Какая причина и на что переходить? Дописать в [Obsolete(\"...\")]";

        public override void Analyze(CheckContext context)
        {
            foreach (var attribute in context.Root.DescendantNodes().OfType<AttributeSyntax>())
            {
                if (TypeNames.GetAttributeName(attribute.Name) != "Obsolete" || HasReason(attribute))
                    continue;

                var owner = attribute.Parent?.Parent;
                var name = owner == null ? "" : GetOwnerName(owner);
                context.Report(attribute.Span, $"[{attribute}] без причины{name}");
            }
        }

        private static bool HasReason(AttributeSyntax attribute)
        {
            var arguments = attribute.ArgumentList?.Arguments;
            if (arguments == null || arguments.Value.Count == 0)
                return false;

            var first = arguments.Value[0].Expression;
            return first is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.StringLiteralExpression)
                || literal.Token.ValueText.Trim().Length > 0;
        }

        private static string GetOwnerName(SyntaxNode owner)
        {
            var name = owner switch
            {
                BaseTypeDeclarationSyntax type => type.Identifier.Text,
                MethodDeclarationSyntax method => method.Identifier.Text,
                PropertyDeclarationSyntax property => property.Identifier.Text,
                FieldDeclarationSyntax field => field.Declaration.Variables[0].Identifier.Text,
                _ => null,
            };

            return name == null ? "" : $": {name}";
        }
    }
}
