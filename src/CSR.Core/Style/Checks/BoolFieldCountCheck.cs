using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Класс-наследник типа из baseTypes (прямой базовый тип в списке наследования) с minFields и больше полями bool
    // экземпляра: набор флагов - кандидат в enum или [Flags]
    public class BoolFieldCountCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings(
                "baseTypes",
                "маски имени базового типа без пространства имен и аргументов типа; пусто - любой класс",
                "MonoBehaviour"),
            CheckParameter.Int("minFields", 3, "сколько полей bool - находка"),
            FileScope.ExcludePaths,
        ];

        public override string Name
            => "bool-field-count";

        public override string Description
            => "много полей bool в классе";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var baseTypes = NameMask.Get(context.Settings.GetStrings("baseTypes"));
            int minFields = context.Settings.GetInt("minFields");
            foreach (var type in context.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!baseTypes.IsEmpty && !SyntaxTools.HasBaseType(type, baseTypes))
                    continue;

                var names = type.Members
                    .OfType<FieldDeclarationSyntax>()
                    .Where(IsBoolField)
                    .SelectMany(field => field.Declaration.Variables)
                    .Select(variable => variable.Identifier.Text)
                    .ToList();
                if (names.Count < minFields)
                    continue;

                var message = $"полей bool в '{type.Identifier.Text}': {names.Count} ({string.Join(", ", names)})";
                context.Report(type.Identifier.Span, message);
            }
        }

        private static bool IsBoolField(FieldDeclarationSyntax field)
        {
            if (field.Modifiers.Any(SyntaxKind.StaticKeyword) || field.Modifiers.Any(SyntaxKind.ConstKeyword))
                return false;

            var type = field.Declaration.Type;
            return type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.BoolKeyword)
                || TypeNameTools.GetRightName(type) == "Boolean";
        }
    }
}
