using CSR.Core.Arch;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Naming
{
    // Объявленные имена файла одним проходом. Имена, заданные базой (override, явная реализация интерфейса, их
    // параметры), не собираются: менять их нельзя. Виды: namespace, class, struct, record, interface, enum, delegate,
    // typeParameter, field, event, property, parameter, local
    public static class NameSymbols
    {
        public static readonly IReadOnlyList<string> Kinds =
        [
            "namespace",
            "class",
            "struct",
            "record",
            "interface",
            "enum",
            "delegate",
            "typeParameter",
            "field",
            "event",
            "property",
            "parameter",
            "local",
        ];

        public static readonly IReadOnlyList<string> Access = ["private", "protected", "internal", "public"];

        public static IEnumerable<NameSymbol> Collect(SyntaxNode root)
        {
            foreach (var node in root.DescendantNodes())
            {
                foreach (var symbol in FromNode(node))
                    yield return symbol;
            }
        }

        private static IEnumerable<NameSymbol> FromNode(SyntaxNode node)
        {
            return node switch
            {
                BaseNamespaceDeclarationSyntax declaration => FromNamespace(declaration),
                BaseTypeDeclarationSyntax type
                    => [FromType(type, type.Identifier, type.Modifiers, type.AttributeLists, type.BaseList)],
                DelegateDeclarationSyntax handler
                    => [FromType(handler, handler.Identifier, handler.Modifiers, handler.AttributeLists, null)],
                TypeParameterSyntax parameter => [Create("typeParameter", parameter.Identifier, "параметр-тип")],
                FieldDeclarationSyntax { Parent: TypeDeclarationSyntax and not InterfaceDeclarationSyntax } field
                    => FromField(field),
                EventFieldDeclarationSyntax field => FromEventField(field),
                EventDeclarationSyntax handler when !IsInherited(handler.Modifiers, handler.ExplicitInterfaceSpecifier)
                    => [FromMember(handler, "event", handler.Identifier, handler.Type, handler.AttributeLists)],
                PropertyDeclarationSyntax property when !IsInherited(property.Modifiers, property.ExplicitInterfaceSpecifier)
                    => [FromMember(property, "property", property.Identifier, property.Type, property.AttributeLists)],
                ParameterSyntax { Type: not null } parameter when !IsInheritedSignature(parameter)
                    => [Create("parameter", parameter.Identifier, "параметр", parameter.Type)],
                LocalDeclarationStatementSyntax local => FromVariables(local.Declaration),
                DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax designation } declaration
                    => [Create("local", designation.Identifier, "локальная", declaration.Type)],
                _ => [],
            };
        }

        private static IEnumerable<NameSymbol> FromEventField(EventFieldDeclarationSyntax field)
        {
            if (IsInherited(field.Modifiers, null))
                return [];

            return field.Declaration.Variables
                .Select(
                    variable => FromMember(field, "event", variable.Identifier, field.Declaration.Type, field.AttributeLists));
        }

        private static IEnumerable<NameSymbol> FromVariables(VariableDeclarationSyntax declaration)
        {
            return declaration.Variables
                .Select(variable => Create("local", variable.Identifier, "локальная", declaration.Type));
        }

        // Пространство с типами (не только с вложенными пространствами): полное имя с внешними объявлениями
        private static IEnumerable<NameSymbol> FromNamespace(BaseNamespaceDeclarationSyntax declaration)
        {
            if (declaration.Members.Count > 0 && declaration.Members.All(member => member is BaseNamespaceDeclarationSyntax))
                return [];

            var name = GetNamespace(declaration);
            return [new NameSymbol { Kind = "namespace", Name = name, Span = declaration.Name.Span, Label = "пространство" }];
        }

        private static NameSymbol FromType(
            MemberDeclarationSyntax type,
            SyntaxToken identifier,
            SyntaxTokenList modifiers,
            SyntaxList<AttributeListSyntax> attributes,
            BaseListSyntax? bases)
        {
            var (kind, label) = type switch
            {
                InterfaceDeclarationSyntax => ("interface", "интерфейс"),
                StructDeclarationSyntax => ("struct", "структура"),
                RecordDeclarationSyntax => ("record", "запись"),
                EnumDeclarationSyntax => ("enum", "enum"),
                DelegateDeclarationSyntax => ("delegate", "delegate"),
                _ => ("class", "класс"),
            };
            var namespaceDeclaration = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            return new NameSymbol
            {
                Kind = kind,
                Name = identifier.Text,
                Span = identifier.Span,
                Label = label,
                Access = GetAccess(type, modifiers),
                Modifiers = GetModifiers(modifiers),
                Attributes = GetAttributes(attributes),
                Bases = bases?.Types.Select(item => ArchSyntax.GetSimpleTypeName(item.Type)).ToList() ?? [],
                IsNested = type.Parent is BaseTypeDeclarationSyntax,
                Namespace = namespaceDeclaration == null ? null : GetNamespace(namespaceDeclaration),
            };
        }

        private static IEnumerable<NameSymbol> FromField(FieldDeclarationSyntax field)
        {
            var modifiers = field.Modifiers;
            bool isConst = modifiers.Any(SyntaxKind.ConstKeyword);
            var access = GetAccess(field, modifiers);
            var kind = modifiers.Any(SyntaxKind.StaticKeyword) ? " статическое поле" : " поле";
            var label = isConst ? "константа" : GetAccessWord(access) + kind;
            foreach (var variable in field.Declaration.Variables)
            {
                yield return new NameSymbol
                {
                    Kind = "field",
                    Name = variable.Identifier.Text,
                    Span = variable.Identifier.Span,
                    Label = label,
                    Access = access,
                    Modifiers = GetModifiers(modifiers),
                    TypeName = GetTypeName(field.Declaration.Type),
                    Attributes = GetAttributes(field.AttributeLists),
                };
            }
        }

        private static NameSymbol FromMember(
            MemberDeclarationSyntax member,
            string kind,
            SyntaxToken identifier,
            TypeSyntax type,
            SyntaxList<AttributeListSyntax> attributes)
        {
            return new NameSymbol
            {
                Kind = kind,
                Name = identifier.Text,
                Span = identifier.Span,
                Label = kind == "event" ? "событие" : "свойство",
                Access = GetAccess(member, member.Modifiers),
                Modifiers = GetModifiers(member.Modifiers),
                TypeName = GetTypeName(type),
                Attributes = GetAttributes(attributes),
            };
        }

        private static NameSymbol Create(string kind, SyntaxToken identifier, string label, TypeSyntax? type = null)
        {
            return new NameSymbol
            {
                Kind = kind,
                Name = identifier.Text,
                Span = identifier.Span,
                Label = label,
                TypeName = type == null ? null : GetTypeName(type),
            };
        }

        // Тип без пространства имен и '?': System.Collections.Generic.List<int>? -> List<int>; var - null
        private static string? GetTypeName(TypeSyntax type)
        {
            if (type.IsVar)
                return null;

            var element = type is NullableTypeSyntax nullable ? nullable.ElementType : type;
            return TypeNames.GetRightName(element);
        }

        // Доступ: protected internal и private protected - protected, file - internal. Без модификатора - по месту:
        // член интерфейса - public, тип вне типа - internal, член типа - private
        private static string GetAccess(MemberDeclarationSyntax member, SyntaxTokenList modifiers)
        {
            if (modifiers.Any(SyntaxKind.PublicKeyword))
                return "public";

            if (modifiers.Any(SyntaxKind.ProtectedKeyword))
                return "protected";

            if (modifiers.Any(SyntaxKind.InternalKeyword) || modifiers.Any(SyntaxKind.FileKeyword))
                return "internal";

            if (modifiers.Any(SyntaxKind.PrivateKeyword))
                return "private";

            return member.Parent switch
            {
                InterfaceDeclarationSyntax => "public",
                BaseTypeDeclarationSyntax => "private",
                _ => "internal",
            };
        }

        private static string GetAccessWord(string access)
        {
            return access switch
            {
                "protected" => "защищенное",
                "internal" => "внутреннее",
                "public" => "открытое",
                _ => "закрытое",
            };
        }

        private static IReadOnlyCollection<string> GetModifiers(SyntaxTokenList modifiers)
            => new HashSet<string>(modifiers.Select(token => token.Text));

        // Атрибуты самого объявления: списки с целью ([field: SerializeField] у автосвойства) относятся к другому
        private static IReadOnlyList<string> GetAttributes(SyntaxList<AttributeListSyntax> lists)
        {
            return lists
                .Where(list => list.Target == null)
                .SelectMany(list => list.Attributes)
                .Select(attribute => Invocations.GetSimpleName(attribute.Name))
                .ToList();
        }

        private static string GetNamespace(BaseNamespaceDeclarationSyntax declaration)
        {
            var names = declaration.AncestorsAndSelf()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Select(ancestor => ancestor.Name.WithoutTrivia().ToString())
                .Reverse();
            return string.Join(".", names);
        }

        private static bool IsOverride(SyntaxTokenList modifiers)
            => modifiers.Any(SyntaxKind.OverrideKeyword);

        // Имя задано базой: override или явная реализация интерфейса
        private static bool IsInherited(SyntaxTokenList modifiers, ExplicitInterfaceSpecifierSyntax? explicitInterface)
            => IsOverride(modifiers) || explicitInterface != null;

        // Параметр override или явной реализации интерфейса: имя задано базой
        private static bool IsInheritedSignature(ParameterSyntax parameter)
        {
            return parameter.Parent?.Parent switch
            {
                MethodDeclarationSyntax method => IsOverride(method.Modifiers) || method.ExplicitInterfaceSpecifier != null,
                IndexerDeclarationSyntax indexer => IsOverride(indexer.Modifiers) || indexer.ExplicitInterfaceSpecifier != null,
                _ => false,
            };
        }
    }
}
