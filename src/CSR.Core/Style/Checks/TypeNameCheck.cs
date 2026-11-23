using CSR.Core.Arch;
using CSR.Core.Checks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Имена типов: префикс интерфейса (IStorage), префикс параметра-типа (T, TItem), имя наследника по маске
    // из derivedNames (база -> маска имени). База - по имени в списке баз объявления, без семантики: наследник
    // через промежуточный тип не проверяется
    public class TypeNameCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Text("interfacePrefix", "I", "префикс интерфейса перед заглавной буквой; пусто - не проверять"),
            CheckParameter.Text(
                "typeParameterPrefix",
                "T",
                "префикс параметра-типа (T или перед заглавной буквой); пусто - не проверять"),
            CheckParameter.Map(
                "derivedNames",
                new Dictionary<string, string>(),
                "маска имени базового типа -> маска имени наследника (\"*Command\": \"*Command\")"),
        ];

        public override string Name
            => "type-name";

        public override string Description
            => "префиксы интерфейсов и параметров-типов, имена наследников";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var interfacePrefix = context.Settings.GetString("interfacePrefix");
            var typeParameterPrefix = context.Settings.GetString("typeParameterPrefix");
            var derivedNames = context.Settings.GetMap("derivedNames");
            foreach (var node in context.Root.DescendantNodes())
            {
                switch (node)
                {
                    case InterfaceDeclarationSyntax declaration when !IsPrefixed(declaration.Identifier.Text, interfacePrefix):
                        context.Report(
                            declaration.Identifier.Span,
                            $"интерфейс '{declaration.Identifier.Text}': префикс '{interfacePrefix}'");
                        break;
                    case TypeParameterSyntax parameter when !IsTypeParameterName(parameter.Identifier.Text, typeParameterPrefix):
                        context.Report(
                            parameter.Identifier.Span,
                            $"параметр-тип '{parameter.Identifier.Text}': префикс '{typeParameterPrefix}'");
                        break;
                    case TypeDeclarationSyntax type when type.BaseList != null && derivedNames.Count > 0:
                        CheckDerived(context, type, type.BaseList, derivedNames);
                        break;
                }
            }
        }

        private static void CheckDerived(
            CheckContext context,
            TypeDeclarationSyntax type,
            BaseListSyntax baseList,
            IReadOnlyDictionary<string, string> derivedNames)
        {
            var name = type.Identifier.Text;
            foreach (var baseType in baseList.Types)
            {
                var baseName = GetSimpleName(baseType.Type);
                foreach (var pair in derivedNames)
                {
                    if (!NameMask.Get([pair.Key]).IsMatch(baseName) || NameMask.Get([pair.Value]).IsMatch(name))
                        continue;

                    context.Report(type.Identifier.Span, $"'{name}' наследует '{baseName}': имя по маске '{pair.Value}'");
                    return;
                }
            }
        }

        // Имя типа без пространства и аргументов: System.Collections.Generic.List<int> -> List
        private static string GetSimpleName(TypeSyntax type)
        {
            return type switch
            {
                QualifiedNameSyntax qualified => GetSimpleName(qualified.Right),
                AliasQualifiedNameSyntax alias => GetSimpleName(alias.Name),
                GenericNameSyntax generic => generic.Identifier.Text,
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                _ => type.ToString(),
            };
        }

        // Префикс перед заглавной буквой или цифрой: IStorage, не Item
        private static bool IsPrefixed(string name, string prefix)
        {
            if (prefix.Length == 0)
                return true;

            return name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.Ordinal)
                && !char.IsLower(name[prefix.Length]);
        }

        private static bool IsTypeParameterName(string name, string prefix)
            => name == prefix || IsPrefixed(name, prefix);
    }
}
