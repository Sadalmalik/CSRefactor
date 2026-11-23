using CSR.Core.Checks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Обращение к запрещенному члену: вызов, свойство, поле. Имя обращения - Тип.Член: последний идентификатор
    // слева от точки и член (UnityEngine.Debug.Log -> Debug.Log); маски из calls, у каждой - текст альтернативы
    public class ForbiddenCallCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Map(
                "calls",
                new Dictionary<string, string>(),
                "маска Тип.Член (* - любые символы) и чем заменить"),
            FileScope.ExcludePaths,
        ];

        public override string Name
            => "forbidden-call";

        public override string Description
            => "запрещенный вызов или член";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var calls = new ForbiddenNames(context.Settings.GetMap("calls"));
            if (calls.IsEmpty || FileScope.IsExcluded(context))
                return;

            foreach (var access in context.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                var owner = GetOwnerName(access.Expression);
                if (owner == null)
                    continue;

                var name = $"{owner}.{access.Name.Identifier.Text}";
                if (calls.TryFind(name, out string use))
                    context.Report(access.Span, ForbiddenNames.WithUse($"запрещенный член '{name}'", use));
            }
        }

        // Последний идентификатор выражения слева от точки: Debug, UnityEngine.Debug -> Debug, Service<T> -> Service,
        // string.Format -> string
        private static string? GetOwnerName(ExpressionSyntax expression)
        {
            return expression switch
            {
                SimpleNameSyntax simple => simple.Identifier.Text,
                PredefinedTypeSyntax predefined => predefined.Keyword.Text,
                MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
                AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
                _ => null,
            };
        }
    }
}
