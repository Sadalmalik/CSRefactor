using System.IO;
using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // partial-файл с подсистемой: часть partial-типа в файле не своего имени (Type.Swim.cs, а не Type.cs),
    // у которой не меньше minFields своих полей и minMethods методов. Аспект из aspects (Type.Gizmos.cs) и
    // часть в файле Type.cs не ищутся: однофайловый partial по требованию генератора не находка
    public class PartialFileCheck : ArchCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings(
                "aspects",
                "маски аспектов файла Type.<аспект>.cs, которые допустимы",
                "Gizmos",
                "Debug",
                "g",
                "Designer",
                "Generated"),
            CheckParameter.Int("minFields", 1, "сколько своих полей в части - находка"),
            CheckParameter.Int("minMethods", 5, "сколько методов в части - находка"),
        ];

        public override string Name
            => "partial-file";

        public override string Description
            => "partial-файл с подсистемой";

        public override string Question
            => "Это обособленный аспект или режим и подсистема? "
                + "Подсистему - отдельным классом за точкой расширения, группы - регионами";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var fileName = Path.GetFileNameWithoutExtension(context.Tree.FilePath);
            var aspects = NameMask.Get(context.Settings.GetStrings("aspects"));
            foreach (var type in context.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var name = type.Identifier.Text;
                if (!type.Modifiers.Any(SyntaxKind.PartialKeyword) || fileName == name)
                    continue;

                bool isOwnAspect = fileName.StartsWith(name + ".", StringComparison.Ordinal);
                if (isOwnAspect && aspects.IsMatch(fileName.Substring(name.Length + 1)))
                    continue;

                CheckPart(context, type, fileName);
            }
        }

        private static void CheckPart(CheckContext context, TypeDeclarationSyntax type, string fileName)
        {
            int fields = type.Members.OfType<FieldDeclarationSyntax>().Sum(field => field.Declaration.Variables.Count);
            int methods = type.Members.OfType<MethodDeclarationSyntax>().Count();
            if (fields < context.Settings.GetInt("minFields") || methods < context.Settings.GetInt("minMethods"))
                return;

            var lines = context.Text.Lines;
            int length = lines.GetLineFromPosition(type.Span.End).LineNumber
                - lines.GetLineFromPosition(type.SpanStart).LineNumber + 1;
            var message = $"часть '{type.Identifier.Text}' в файле {fileName}.cs: полей {fields}, методов {methods}, "
                + $"строк {length}";
            context.Report(type.Identifier.Span, message);
        }
    }
}
