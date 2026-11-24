using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSR.Core.Checks;

namespace CSR.Core.Syntax
{
    // Общие разборы синтаксиса для проверок style и arch: имена типов и атрибутов, видимость, базовые типы
    public static class SyntaxParts
    {
        // Имя типа без пространства имен, аргументов типа и '?': List<int> -> List, A.B -> B
        public static string GetSimpleTypeName(TypeSyntax type)
        {
            return type switch
            {
                QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
                AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
                SimpleNameSyntax simple => simple.Identifier.Text,
                NullableTypeSyntax nullable => GetSimpleTypeName(nullable.ElementType),
                _ => type.ToString(),
            };
        }

        // Текст типа без пробелов и без '?' в конце
        public static string GetTypeText(TypeSyntax type)
        {
            var text = new string(type.ToString().Where(symbol => !char.IsWhiteSpace(symbol)).ToArray());
            return text.TrimEnd('?');
        }

        // Имя по соглашению об интерфейсах: I и заглавная буква
        public static bool IsInterfaceName(string name)
            => name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]);

        public static bool IsIntegerType(TypeSyntax type)
        {
            var element = type is NullableTypeSyntax nullable ? nullable.ElementType : type;
            return element is PredefinedTypeSyntax predefined && predefined.Keyword.Kind() is SyntaxKind.IntKeyword
                or SyntaxKind.UIntKeyword or SyntaxKind.LongKeyword or SyntaxKind.ULongKeyword
                or SyntaxKind.ShortKeyword or SyntaxKind.UShortKeyword or SyntaxKind.ByteKeyword
                or SyntaxKind.SByteKeyword;
        }

        public static bool IsNumericType(TypeSyntax type)
        {
            if (IsIntegerType(type))
                return true;

            return type is PredefinedTypeSyntax predefined && predefined.Keyword.Kind() is SyntaxKind.FloatKeyword
                or SyntaxKind.DoubleKeyword or SyntaxKind.DecimalKeyword;
        }

        // Член виден снаружи типа: public или internal, член интерфейса без private
        public static bool IsApi(MemberDeclarationSyntax member)
        {
            var modifiers = member.Modifiers;
            if (modifiers.Any(SyntaxKind.PublicKeyword) || modifiers.Any(SyntaxKind.InternalKeyword))
                return !modifiers.Any(SyntaxKind.PrivateKeyword);

            return member.Parent is InterfaceDeclarationSyntax && !modifiers.Any(SyntaxKind.PrivateKeyword);
        }

        // Тип объявлен наследником одного из базовых типов по маске простого имени
        public static bool HasBaseType(TypeDeclarationSyntax type, NameMask baseTypes)
        {
            if (type.BaseList == null)
                return false;

            return type.BaseList.Types.Any(baseType => baseTypes.IsMatch(GetSimpleTypeName(baseType.Type)));
        }

        // Имя метода, конструктора, свойства или локальной функции, в которых находится узел; null - вне членов
        public static string? GetEnclosingMemberName(SyntaxNode node)
        {
            foreach (var ancestor in node.Ancestors())
            {
                switch (ancestor)
                {
                    case MethodDeclarationSyntax method:
                        return method.Identifier.Text;
                    case ConstructorDeclarationSyntax constructor:
                        return constructor.Identifier.Text;
                    case PropertyDeclarationSyntax property:
                        return property.Identifier.Text;
                    case TypeDeclarationSyntax:
                        return null;
                }
            }

            return null;
        }

        // Тип, которому принадлежит узел
        public static TypeDeclarationSyntax? GetEnclosingType(SyntaxNode node)
            => node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();

        // Перечисление имен для сообщения: не больше limit, остальное - числом
        public static string JoinNames(IReadOnlyList<string> names, int limit = 8)
        {
            if (names.Count <= limit)
                return string.Join(", ", names);

            return string.Join(", ", names.Take(limit)) + $" и еще {names.Count - limit}";
        }

        // Текст узла в одну строку, не длиннее limit
        public static string Shorten(SyntaxNode node, int limit = 80)
        {
            var text = string.Join(" ", node.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length <= limit ? text : text.Substring(0, limit - 3) + "...";
        }

        // Имя атрибута без пространства имен и суффикса Attribute
        public static string GetAttributeName(NameSyntax name)
            => TrimAttributeSuffix(GetSimpleTypeName(name));

        public static string TrimAttributeSuffix(string name)
            => name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - 9) : name;

        // У члена есть атрибут с одним из имен (без суффикса Attribute)
        public static bool HasAttribute(MemberDeclarationSyntax member, IReadOnlyList<string> names)
        {
            return member.AttributeLists
                .SelectMany(list => list.Attributes)
                .Any(attribute => names.Contains(GetAttributeName(attribute.Name)));
        }

        // Начало объявления после атрибутов: первый модификатор или тип; от его строки считается отступ
        public static int GetDeclarationStart(SyntaxNode member)
        {
            var first = member.DescendantTokens()
                .First(token => !token.Parent!.AncestorsAndSelf().OfType<AttributeListSyntax>().Any());
            return first.SpanStart;
        }

        // Узел внутри объявления символа
        public static bool IsInside(SyntaxNode node, ISymbol symbol)
        {
            return symbol.DeclaringSyntaxReferences.Any(reference => reference.SyntaxTree == node.SyntaxTree
                && reference.Span.Contains(node.Span));
        }

        // Комментарий, в том числе документирующий
        public static bool IsComment(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);
        }
    }
}
