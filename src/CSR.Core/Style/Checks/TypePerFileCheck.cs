using CSR.Core.Arch;
using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Несколько типов верхнего уровня в файле, где есть наследник типа из baseTypes: находка на каждом типе,
    // имя которого не совпадает с именем файла, с командой переноса csr move
    public class TypePerFileCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings(
                "baseTypes",
                "маски имени базового типа без пространства имен и аргументов типа; пусто - любой файл с несколькими типами",
                "MonoBehaviour",
                "ScriptableObject"),
            FileScope.ExcludePaths,
        ];

        public override string Name
            => "type-per-file";

        public override string Description
            => "несколько типов в файле";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            if (FileScope.IsExcluded(context))
                return;

            var types = new List<(MemberDeclarationSyntax Type, SyntaxToken Identifier)>();
            foreach (var member in context.Root.DescendantNodes(IsTypeContainer).OfType<MemberDeclarationSyntax>())
            {
                if (member is BaseTypeDeclarationSyntax declaration)
                    types.Add((member, declaration.Identifier));
                else if (member is DelegateDeclarationSyntax handler)
                    types.Add((member, handler.Identifier));
            }

            if (types.Count < 2)
                return;

            var baseTypes = NameMask.Get(context.Settings.GetStrings("baseTypes"));
            bool hasDerived = baseTypes.IsEmpty
                || types.Any(item => item.Type is TypeDeclarationSyntax type && ArchSyntax.HasBaseType(type, baseTypes));
            if (!hasDerived)
                return;

            var fileName = Path.GetFileNameWithoutExtension(context.Tree.FilePath);
            foreach (var (type, identifier) in types)
            {
                if (identifier.Text == fileName)
                    continue;

                var name = GetFullName(type, identifier.Text);
                context.Report(identifier.Span, $"тип '{identifier.Text}' не один в файле {fileName}.cs: csr move {name}");
            }
        }

        private static bool IsTypeContainer(SyntaxNode node)
            => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax;

        private static string GetFullName(MemberDeclarationSyntax type, string identifier)
        {
            var namespaces = type.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Reverse()
                .Select(declaration => declaration.Name.ToString());
            return string.Join(".", namespaces.Append(identifier));
        }
    }
}
