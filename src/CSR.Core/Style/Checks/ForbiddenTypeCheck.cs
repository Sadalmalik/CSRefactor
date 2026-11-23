using CSR.Core.Checks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Запрещенный тип в позиции типа: объявление, параметр, результат, аргумент типа, new, приведение, typeof.
    // Имя - правая часть без пространства имен (System.Collections.ArrayList -> ArrayList, List<int> -> List<int>),
    // встроенный тип - ключевым словом (object); маски из types, у каждой - текст альтернативы. Массив целиком
    // (int[]) не сравнивается: сравнивается тип элемента
    public class ForbiddenTypeCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Map(
                "types",
                new Dictionary<string, string>(),
                "маска имени типа (* - любые символы) и чем заменить"),
            FileScope.ExcludePaths,
        ];

        public override string Name
            => "forbidden-type";

        public override string Description
            => "запрещенный тип";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var types = new ForbiddenNames(context.Settings.GetMap("types"));
            if (types.IsEmpty || FileScope.IsExcluded(context))
                return;

            foreach (var type in context.Root.DescendantNodes().OfType<TypeSyntax>())
            {
                if (type is not (NameSyntax or PredefinedTypeSyntax)
                    || type.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax)
                    continue;

                if (!SyntaxFacts.IsInTypeOnlyContext(type))
                    continue;

                var name = TypeNames.GetRightName(type);
                if (types.TryFind(name, out string use))
                    context.Report(type.Span, ForbiddenNames.WithUse($"запрещенный тип '{name}'", use));
            }
        }
    }
}
