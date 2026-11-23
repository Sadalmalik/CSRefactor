using CSR.Core.Arch;
using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Лямбда или анонимный метод внутри метода из methods (Update), захватывающие параметры или локальные
    // переменные метода: замыкание создается на каждом вызове. По синтаксису: имя из объявлений метода вне лямбды,
    // использованное в лямбде (const и nameof - не захват); с captureThis - и явный this / base. static-лямбды
    // не проверяются; вложенные лямбды входят в находку внешней
    public class ClosureInMethodCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings(
                "methods",
                "маски имен методов, где замыкания - находка",
                "Update",
                "LateUpdate",
                "FixedUpdate"),
            CheckParameter.Bool("captureThis", false, "явный this или base в лямбде - тоже захват"),
            FileScope.ExcludePaths,
        ];

        public override string Name
            => "closure-in-method";

        public override string Description
            => "лямбда с захватом в часто вызываемом методе";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            if (FileScope.IsExcluded(context))
                return;

            var methods = NameMask.Get(context.Settings.GetStrings("methods"));
            bool captureThis = context.Settings.GetBool("captureThis");
            foreach (var method in context.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (!methods.IsMatch(method.Identifier.Text))
                    continue;

                var declared = LocalNames.GetDeclared(method);
                declared.ExceptWith(LocalNames.GetConstants(method));
                foreach (var lambda in method.DescendantNodes().OfType<AnonymousFunctionExpressionSyntax>())
                {
                    if (lambda.Modifiers.Any(SyntaxKind.StaticKeyword) || IsNested(lambda, method))
                        continue;

                    var captured = GetCaptured(lambda, declared, captureThis);
                    if (captured.Count == 0)
                        continue;

                    var message = $"лямбда в '{method.Identifier.Text}' захватывает {string.Join(", ", captured)}";
                    context.Report(lambda.Span, message);
                }
            }
        }

        // Вложенная лямбда: ее захват уже в находке внешней
        private static bool IsNested(AnonymousFunctionExpressionSyntax lambda, MethodDeclarationSyntax method)
        {
            return lambda.Ancestors()
                .TakeWhile(ancestor => ancestor != method)
                .OfType<AnonymousFunctionExpressionSyntax>()
                .Any();
        }

        private static List<string> GetCaptured(
            AnonymousFunctionExpressionSyntax lambda,
            HashSet<string> declared,
            bool captureThis)
        {
            var own = LocalNames.GetDeclared(lambda);
            var captured = LocalNames.GetUsed(lambda)
                .Where(name => declared.Contains(name) && !own.Contains(name))
                .Distinct()
                .ToList();

            if (captureThis && lambda.DescendantNodes().Any(node => node is ThisExpressionSyntax or BaseExpressionSyntax))
                captured.Add("this");

            return captured;
        }
    }
}
