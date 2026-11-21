using CSR.Core.Checks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Объявление своего delegate вместо Action / Func
    public class CustomDelegateCheck : SyntaxCheck
    {
        public override string Name
            => "custom-delegate";

        public override string Description
            => "свой delegate";

        public override void Analyze(CheckContext context)
        {
            foreach (var declaration in context.Root.DescendantNodes().OfType<DelegateDeclarationSyntax>())
                context.Report(declaration.Identifier.Span, $"свой delegate '{declaration.Identifier.Text}'");
        }
    }
}
