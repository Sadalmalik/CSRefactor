using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch
{
    // Общие разборы синтаксиса для признаков: слова имен, имена типов, видимость, базовые типы
    public static class ArchSyntax
    {
        private static readonly string[] FieldPrefixes = ["m_", "s_", "k_", "_"];

        // Слова идентификатора без префикса поля: m_EnterRadius -> Enter, Radius; cellIndex -> cell, Index;
        // MAX_SPEED -> MAX, SPEED
        public static List<string> SplitWords(string name)
        {
            var prefix = FieldPrefixes
                .FirstOrDefault(candidate => name.StartsWith(candidate, StringComparison.Ordinal));
            var text = prefix == null ? name : name.Substring(prefix.Length);
            var words = new List<string>();
            var word = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                var symbol = text[i];
                if (symbol == '_' || !char.IsLetterOrDigit(symbol))
                {
                    Flush(words, word);
                    continue;
                }

                if (word.Length > 0 && IsWordStart(text, i))
                    Flush(words, word);

                word.Append(symbol);
            }

            Flush(words, word);
            return words;
        }

        // Есть ли в словах имени фраза подряд (DeadZone -> Dead, Zone), без учета регистра
        public static bool ContainsPhrase(IReadOnlyList<string> words, string phrase)
        {
            var parts = SplitWords(phrase);
            if (parts.Count == 0)
                return false;

            for (int start = 0; start + parts.Count <= words.Count; start++)
            {
                bool isMatch = true;
                for (int i = 0; i < parts.Count && isMatch; i++)
                    isMatch = string.Equals(words[start + i], parts[i], StringComparison.OrdinalIgnoreCase);

                if (isMatch)
                    return true;
            }

            return false;
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

        private static bool IsWordStart(string text, int index)
        {
            var symbol = text[index];
            var previous = text[index - 1];
            if (char.IsDigit(symbol) != char.IsDigit(previous))
                return true;

            if (!char.IsUpper(symbol))
                return false;

            if (char.IsLower(previous))
                return true;

            return index + 1 < text.Length && char.IsLower(text[index + 1]) && char.IsUpper(previous);
        }

        private static void Flush(List<string> words, StringBuilder word)
        {
            if (word.Length == 0)
                return;

            words.Add(word.ToString());
            word.Clear();
        }
    }
}
