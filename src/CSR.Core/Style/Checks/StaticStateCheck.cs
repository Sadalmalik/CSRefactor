using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Статическое изменяемое состояние: static-поле без readonly и const, static readonly поле и static автосвойство
    // только с get изменяемого типа (mutableTypes: коллекции, массивы), static автосвойство с set, static event;
    // синглтон - static поле или свойство своего типа (в том числе readonly и только get). Исключения - атрибуты
    // члена или типа, маски имен членов
    public class StaticStateCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings(
                "mutableTypes",
                "маски типов static readonly полей и get-свойств, содержимое которых меняется (без пространства имен)",
                "List<*>",
                "Dictionary<*>",
                "HashSet<*>",
                "Queue<*>",
                "Stack<*>",
                "*[]"),
            CheckParameter.Bool("singleton", true, "static поле или свойство своего типа"),
            CheckParameter.Strings(
                "ignoreAttributes",
                "атрибуты члена или его типа, при которых член не проверяется (без суффикса Attribute)"),
            CheckParameter.Strings("ignoreNames", "маски имен членов, которые не проверяются"),
            CheckParameter.Text("use", "", "чем заменить: текст в сообщении"),
            FileScope.ExcludePaths,
        ];

        public override string Name
            => "static-state";

        public override string Description
            => "статическое изменяемое состояние или синглтон";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            foreach (var member in context.Root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                if (!member.Modifiers.Any(SyntaxKind.StaticKeyword) || member.Parent is not TypeDeclarationSyntax owner)
                    continue;

                var problem = member switch
                {
                    FieldDeclarationSyntax field => GetFieldProblem(context, field, owner),
                    PropertyDeclarationSyntax property => GetPropertyProblem(context, property, owner),
                    EventFieldDeclarationSyntax handler => new Problem(
                        handler.Declaration.Variables[0].Identifier.Span,
                        $"статическое событие '{GetNames(handler.Declaration)}'"),
                    _ => null,
                };
                if (problem == null || IsIgnored(context, member, owner))
                    continue;

                var use = context.Settings.GetString("use");
                context.Report(problem.Value.Span, ForbiddenNames.WithUse(problem.Value.Message, use));
            }
        }

        private static Problem? GetFieldProblem(CheckContext context, FieldDeclarationSyntax field, TypeDeclarationSyntax owner)
        {
            if (field.Modifiers.Any(SyntaxKind.ConstKeyword))
                return null;

            var variable = field.Declaration.Variables[0];
            var names = GetNames(field.Declaration);
            if (IsSingleton(context, field.Declaration.Type, owner))
                return new Problem(variable.Identifier.Span, $"синглтон '{names}' типа {owner.Identifier.Text}");

            if (!field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
                return new Problem(variable.Identifier.Span, $"статическое изменяемое поле '{names}'");

            var type = TypeNames.GetRightName(field.Declaration.Type);
            if (!IsMutableType(context, type))
                return null;

            return new Problem(variable.Identifier.Span, $"статическое поле '{names}' изменяемого типа {type}");
        }

        private static Problem? GetPropertyProblem(
            CheckContext context,
            PropertyDeclarationSyntax property,
            TypeDeclarationSyntax owner)
        {
            if (IsSingleton(context, property.Type, owner))
                return new Problem(
                    property.Identifier.Span,
                    $"синглтон '{property.Identifier.Text}' типа {owner.Identifier.Text}");

            var accessors = property.AccessorList?.Accessors;
            bool isAuto = accessors?.All(accessor => accessor.Body == null && accessor.ExpressionBody == null) == true;
            bool hasSetter = accessors?.Any(accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration)) == true;
            if (!isAuto)
                return null;

            var name = property.Identifier.Text;
            if (hasSetter)
                return new Problem(property.Identifier.Span, $"статическое свойство '{name}' с set");

            var type = TypeNames.GetRightName(property.Type);
            if (!IsMutableType(context, type))
                return null;

            return new Problem(property.Identifier.Span, $"статическое свойство '{name}' изменяемого типа {type}");
        }

        private static bool IsMutableType(CheckContext context, string type)
            => NameMask.Get(context.Settings.GetStrings("mutableTypes")).IsMatch(type);

        private static string GetNames(VariableDeclarationSyntax declaration)
            => string.Join(", ", declaration.Variables.Select(item => item.Identifier.Text));

        private static bool IsSingleton(CheckContext context, TypeSyntax type, TypeDeclarationSyntax owner)
        {
            if (!context.Settings.GetBool("singleton"))
                return false;

            return SyntaxParts.GetSimpleTypeName(type) == owner.Identifier.Text;
        }

        private static bool IsIgnored(CheckContext context, MemberDeclarationSyntax member, TypeDeclarationSyntax owner)
        {
            var attributes = NameMask.Get(context.Settings.GetStrings("ignoreAttributes"));
            var attributeNames = member.AttributeLists
                .Concat(owner.AttributeLists)
                .SelectMany(list => list.Attributes)
                .Select(attribute => SyntaxParts.GetAttributeName(attribute.Name));
            if (attributeNames.Any(attributes.IsMatch))
                return true;

            var ignoredNames = NameMask.Get(context.Settings.GetStrings("ignoreNames"));
            return member switch
            {
                FieldDeclarationSyntax field => field.Declaration.Variables
                    .All(item => ignoredNames.IsMatch(item.Identifier.Text)),
                PropertyDeclarationSyntax property => ignoredNames.IsMatch(property.Identifier.Text),
                EventFieldDeclarationSyntax handler => handler.Declaration.Variables
                    .All(item => ignoredNames.IsMatch(item.Identifier.Text)),
                _ => false,
            };
        }

        private readonly record struct Problem(TextSpan Span, string Message);
    }
}
