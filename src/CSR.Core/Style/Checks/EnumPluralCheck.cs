using CSR.Core.Checks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Число в имени enum: с [Flags] - множественное (DamageTypes), без - единственное (DamageType).
    // Эвристика по последнему слову: на 's', кроме singularEndings (Status, Class), или из pluralWords
    public class EnumPluralCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("pluralWords", "слова во множественном числе без 's' на конце", "Mask"),
            CheckParameter.Strings("singularEndings", "окончания на 's' в единственном числе", "ss", "us", "is"),
        ];

        public override string Name
            => "enum-plural";

        public override string Description
            => "число в имени enum по [Flags]";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var pluralWords = context.Settings.GetStrings("pluralWords");
            var singularEndings = context.Settings.GetStrings("singularEndings");
            foreach (var declaration in context.Root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                var name = declaration.Identifier.Text;
                bool isPlural = NameWords.IsPlural(NameWords.GetLastWord(name), pluralWords, singularEndings);
                bool isFlags = declaration.AttributeLists
                    .SelectMany(list => list.Attributes)
                    .Any(attribute => IsFlags(attribute.Name));
                if (isFlags && !isPlural)
                    context.Report(declaration.Identifier.Span, $"enum '{name}' с [Flags]: имя во множественном числе");
                else if (!isFlags && isPlural)
                    context.Report(declaration.Identifier.Span, $"enum '{name}' без [Flags]: имя в единственном числе");
            }
        }

        private static bool IsFlags(NameSyntax name)
        {
            var simple = name is QualifiedNameSyntax qualified ? qualified.Right : name;
            var text = simple.ToString();
            return text is "Flags" or "FlagsAttribute";
        }
    }
}
