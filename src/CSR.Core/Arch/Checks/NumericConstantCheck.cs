using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // Поведенческое число константой: const (поле или локальная) или static readonly числового типа,
    // в словах имени которого есть подсказка из hints (Radius, Timeout, Speed, Angle...)
    public class NumericConstantCheck : ArchCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings(
                "hints",
                "слова имени поведенческого числа (DeadZone - два слова подряд)",
                "Radius",
                "Distance",
                "Range",
                "Speed",
                "Velocity",
                "Acceleration",
                "Angle",
                "Timeout",
                "Delay",
                "Duration",
                "Interval",
                "Cooldown",
                "Time",
                "Threshold",
                "DeadZone",
                "Height",
                "Force",
                "Chance"),
            CheckParameter.Bool("staticReadonly", true, "искать и static readonly поля"),
        ];

        public override string Name
            => "numeric-constant";

        public override string Description
            => "поведенческое число константой";

        public override string Question
            => "Это число подбирает дизайнер? Тогда - поле настроек системы с подсказкой; "
                + "технический допуск - оставить const";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            bool isReadonlyIncluded = context.Settings.GetBool("staticReadonly");
            foreach (var node in context.Root.DescendantNodes())
            {
                var declaration = node switch
                {
                    FieldDeclarationSyntax field when IsConstant(field.Modifiers, isReadonlyIncluded)
                        => field.Declaration,
                    LocalDeclarationStatementSyntax local when local.IsConst => local.Declaration,
                    _ => null,
                };

                if (declaration == null || !ArchSyntax.IsNumericType(declaration.Type))
                    continue;

                foreach (var variable in declaration.Variables)
                    CheckVariable(context, declaration, variable);
            }
        }

        private static void CheckVariable(
            CheckContext context,
            VariableDeclarationSyntax declaration,
            VariableDeclaratorSyntax variable)
        {
            var words = ArchSyntax.SplitWords(variable.Identifier.Text);
            var hint = context.Settings.GetStrings("hints")
                .FirstOrDefault(phrase => ArchSyntax.ContainsPhrase(words, phrase));
            if (hint == null)
                return;

            var value = variable.Initializer == null ? "" : " = " + ArchSyntax.Shorten(variable.Initializer.Value, 40);
            context.Report(
                variable.Identifier.Span,
                $"константа {declaration.Type} {variable.Identifier.Text}{value} (подсказка '{hint}')");
        }

        private static bool IsConstant(SyntaxTokenList modifiers, bool isReadonlyIncluded)
        {
            if (modifiers.Any(SyntaxKind.ConstKeyword))
                return true;

            return isReadonlyIncluded && modifiers.Any(SyntaxKind.StaticKeyword)
                && modifiers.Any(SyntaxKind.ReadOnlyKeyword);
        }
    }
}
