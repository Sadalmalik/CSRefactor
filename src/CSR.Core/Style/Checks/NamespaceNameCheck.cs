using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Пространство имен с типами: корень из roots, без слов из forbiddenWords (в любой части, по словам имени),
    // не глубже maxDepth частей. При заданных roots тип вне пространства имен - находка
    public class NamespaceNameCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("roots", "допустимые корни (Company.Product); пусто - не проверять"),
            CheckParameter.Strings("forbiddenWords", "запрещенные слова в частях пространства (без учета регистра)"),
            CheckParameter.Int("maxDepth", 0, "предельное число частей; 0 - не проверять"),
        ];

        public override string Name
            => "namespace-name";

        public override string Description
            => "корень, слова и глубина пространства имен";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var roots = context.Settings.GetStrings("roots");
            var forbidden = context.Settings.GetStrings("forbiddenWords");
            int maxDepth = context.Settings.GetInt("maxDepth");
            if (roots.Count > 0 && context.Root is CompilationUnitSyntax unit)
            {
                foreach (var type in unit.Members.OfType<BaseTypeDeclarationSyntax>())
                    context.Report(type.Identifier.Span, $"тип '{type.Identifier.Text}' вне пространства имен");
            }

            foreach (var declaration in context.Root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
            {
                if (declaration.Members.Count > 0 && declaration.Members.All(member => member is BaseNamespaceDeclarationSyntax))
                    continue;

                var name = GetFullName(declaration);
                var parts = name.Split('.');
                var span = declaration.Name.Span;
                if (roots.Count > 0 && !roots.Any(root => name == root || name.StartsWith(root + ".", StringComparison.Ordinal)))
                    context.Report(span, $"пространство '{name}' вне корня: {string.Join(", ", roots)}");

                var word = NameWords.FindWord(parts.SelectMany(NameWords.Split), forbidden);
                if (word != null)
                    context.Report(span, $"пространство '{name}': слово '{word}'");

                if (maxDepth > 0 && parts.Length > maxDepth)
                    context.Report(span, $"пространство '{name}': частей {parts.Length}, предел {maxDepth}");
            }
        }

        // Полное имя с внешними объявлениями: namespace A { namespace B.C } -> A.B.C
        private static string GetFullName(BaseNamespaceDeclarationSyntax declaration)
        {
            var names = declaration.AncestorsAndSelf()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Select(ancestor => ancestor.Name.WithoutTrivia().ToString())
                .Reverse();
            return string.Join(".", names);
        }
    }
}
