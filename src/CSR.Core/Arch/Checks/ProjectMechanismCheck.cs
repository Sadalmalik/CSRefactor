using CSR.Core.Checks;
using CSR.Core.Config;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // Повтор механизма, который в проекте уже есть (пул, логгер, таймеры): механизмы и их признаки - в project.jsonc
    // (mechanisms, logger.bypass). Тип-повтор - по имени или по полю и методам; вызов и new - в обход механизма
    public class ProjectMechanismCheck : ArchCheck
    {
        public override string Name
            => "project-mechanism";

        public override string Description
            => "повтор механизма проекта";

        public override string Question
            => "В проекте уже есть механизм для этой задачи - заменить им? Если свой нужен - назвать причину";

        public override void Analyze(CheckContext context)
        {
            foreach (var mechanism in context.Project.Mechanisms)
            {
                if (FileScope.IsMatch(mechanism.ExcludePaths, context.Tree.FilePath))
                    continue;

                foreach (var type in context.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                    CheckType(context, mechanism, type);

                CheckCalls(context, mechanism);
            }
        }

        private static void CheckType(CheckContext context, ProjectMechanism mechanism, TypeDeclarationSyntax type)
        {
            var name = type.Identifier.Text;
            var reason = NameMask.Get(mechanism.Types).IsMatch(name) ? "имя" : GetMemberReason(mechanism, type);
            if (reason == null)
                return;

            var message = $"свой '{mechanism.Name}' в '{name}' ({reason}); в проекте: {mechanism.Use}";
            context.Report(type.Identifier.Span, message);
        }

        // Поле и методы повтора: поле по маске Fields (если задана), метод на каждую маску Methods (если заданы)
        private static string? GetMemberReason(ProjectMechanism mechanism, TypeDeclarationSyntax type)
        {
            if (mechanism.Fields.Count == 0 && mechanism.Methods.Count == 0)
                return null;

            var parts = new List<string>();
            if (mechanism.Fields.Count > 0)
            {
                var fields = NameMask.Get(mechanism.Fields);
                var field = type.Members
                    .OfType<FieldDeclarationSyntax>()
                    .Select(declaration => TypeNames.GetTypeText(declaration.Declaration.Type))
                    .FirstOrDefault(fields.IsMatch);
                if (field == null)
                    return null;

                parts.Add("поле " + field);
            }

            var methodNames = type.Members
                .OfType<MethodDeclarationSyntax>()
                .Select(method => method.Identifier.Text)
                .ToList();
            var matched = new List<string>();
            foreach (var mask in mechanism.Methods)
            {
                var pattern = NameMask.Get(new[] { mask });
                var method = methodNames.FirstOrDefault(pattern.IsMatch);
                if (method == null)
                    return null;

                matched.Add(method);
            }

            if (matched.Count > 0)
                parts.Add("методы " + string.Join(", ", matched));

            return string.Join(", ", parts);
        }

        private static void CheckCalls(CheckContext context, ProjectMechanism mechanism)
        {
            if (mechanism.Calls.Count == 0 && mechanism.Creates.Count == 0)
                return;

            var calls = NameMask.Get(mechanism.Calls);
            var creates = NameMask.Get(mechanism.Creates);
            foreach (var node in context.Root.DescendantNodes())
            {
                var text = node switch
                {
                    InvocationExpressionSyntax invocation => Matched(calls, Invocations.GetTargetText(invocation)),
                    ObjectCreationExpressionSyntax creation => Matched(creates, TypeNames.GetTypeText(creation.Type)),
                    _ => null,
                };

                if (text != null)
                    context.Report(node.Span, $"'{text}' в обход '{mechanism.Name}'; в проекте: {mechanism.Use}");
            }
        }

        private static string? Matched(NameMask mask, string text)
            => mask.IsMatch(text) ? text : null;
    }
}
