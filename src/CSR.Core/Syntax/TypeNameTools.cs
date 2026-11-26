using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Syntax
{
    // Имена типов по синтаксису: правая часть, простое имя, текст, интерфейс, числовой тип, имя атрибута
    public static class TypeNameTools
    {
        // Правая часть имени без пространства имен и пробелов: System.Collections.ArrayList -> ArrayList,
        // List< int > -> List<int>, int[] -> int[]
        public static string GetRightName(TypeSyntax type)
        {
            TypeSyntax right = type switch
            {
                QualifiedNameSyntax qualified => qualified.Right,
                AliasQualifiedNameSyntax alias => alias.Name,
                _ => type,
            };

            return string.Concat(right.ToString().Where(symbol => !char.IsWhiteSpace(symbol)));
        }

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

        // Имя атрибута без пространства имен и суффикса Attribute
        public static string GetAttributeName(NameSyntax name)
            => TrimAttributeSuffix(GetSimpleTypeName(name));

        public static string TrimAttributeSuffix(string name)
            => name.EndsWith("Attribute", StringComparison.Ordinal) ? name.Substring(0, name.Length - 9) : name;
    }
}
