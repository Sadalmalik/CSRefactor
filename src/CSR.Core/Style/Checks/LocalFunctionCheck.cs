using CSR.Core.Checks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Локальная функция. Редакторный и тестовый код исключается масками путей (excludePaths)
    public class LocalFunctionCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Text("use", "", "чем заменить: текст в сообщении"),
            FileScope.ExcludePaths,
        ];

        public override string Name
            => "local-function";

        public override string Description
            => "локальная функция";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var use = context.Settings.GetString("use");
            foreach (var function in context.Root.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
            {
                var message = $"локальная функция '{function.Identifier.Text}'";
                context.Report(function.Identifier.Span, ForbiddenNameTool.WithUse(message, use));
            }
        }
    }
}
