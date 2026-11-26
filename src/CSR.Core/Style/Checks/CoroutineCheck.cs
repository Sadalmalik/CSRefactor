using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Метод-корутина: результат из returnTypes (IEnumerator) и, с requireYield, yield в теле метода
    // (не во вложенной локальной функции или лямбде). Методы и локальные функции; GetEnumerator коллекции - не корутина
    public class CoroutineCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings(
                "returnTypes",
                "маски типа результата без пространства имен (IEnumerator, IEnumerable<*>)",
                "IEnumerator"),
            CheckParameter.Bool("requireYield", true, "только с yield в теле; false - любой метод с таким результатом"),
            CheckParameter.Text("use", "", "чем заменить: текст в сообщении"),
            FileScope.ExcludePaths,
        ];

        public override string Name
            => "coroutine";

        public override string Description
            => "метод-корутина";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var returnTypes = NameMask.Get(context.Settings.GetStrings("returnTypes"));
            bool requireYield = context.Settings.GetBool("requireYield");
            var use = context.Settings.GetString("use");
            foreach (var node in context.Root.DescendantNodes())
            {
                var (returnType, identifier) = node switch
                {
                    MethodDeclarationSyntax method => (method.ReturnType, method.Identifier),
                    LocalFunctionStatementSyntax local => (local.ReturnType, local.Identifier),
                    _ => (null, default(SyntaxToken)),
                };
                if (returnType == null || !returnTypes.IsMatch(TypeNameTools.GetRightName(returnType)))
                    continue;

                if (identifier.Text == "GetEnumerator")
                    continue;

                if (requireYield && !HasOwnYield(node))
                    continue;

                var message = $"корутина '{identifier.Text}' ({returnType})";
                context.Report(identifier.Span, ForbiddenNameTool.WithUse(message, use));
            }
        }

        // yield самого метода: вложенные локальные функции и лямбды - свои итераторы
        private static bool HasOwnYield(SyntaxNode method)
        {
            return method
                .DescendantNodes(node => node == method
                    || node is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax))
                .OfType<YieldStatementSyntax>()
                .Any();
        }
    }
}
