using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Similar
{
    // Форма кода по образцу dryer: нормализованное дерево, где остаются вид узла (оператор, вид инструкции), имя
    // вызываемого метода и тип в new; локальные имена, параметры, поля, имена типов вне new и литералы - заглушки.
    // Отпечатки - хеши всех поддеревьев и листьев; две формы сравниваются коэффициентом Жаккара.
    // Решения для C#: this.X - как X; nameof(x) - литерал; текст интерполяции - литерал, выражения в ней - форма;
    // аргументы типа - заглушка типа (List<int> и List<string> в объявлении одинаковы); атрибуты не входят
    public class ShapeHasher
    {
        private const string SymbolLeaf = "symbol";
        private const string LiteralLeaf = "literal";
        private const string TypeLeaf = "type";

        private readonly HashSet<ulong> m_Fingerprints = new();

        public int NodeCount { get; private set; }

        public IReadOnlyCollection<ulong> Fingerprints
            => m_Fingerprints;

        // Хеш формы узла; отпечатки и число узлов накапливаются
        public ulong Add(SyntaxNode node)
            => Normalize(node, false) ?? Leaf("empty");

        // Хеш списка форм под общим узлом (окно операторов)
        public ulong AddGroup(string label, IEnumerable<ulong> children)
            => Combine(label, children);

        public ulong[] GetSortedFingerprints()
        {
            var result = m_Fingerprints.ToArray();
            Array.Sort(result);
            return result;
        }

        public static ulong HashText(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char symbol in text)
            {
                hash ^= symbol;
                hash *= 1099511628211UL;
            }

            return Mix(hash);
        }

        private ulong? Normalize(SyntaxNode node, bool isHead)
        {
            switch (node)
            {
                case AttributeListSyntax:
                    return null;
                case ParenthesizedExpressionSyntax parenthesized:
                    return Normalize(parenthesized.Expression, isHead);
                case MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access:
                    return Normalize(access.Name, isHead);
                case LiteralExpressionSyntax:
                    return Leaf(LiteralLeaf);
                case InterpolatedStringTextSyntax:
                    return Leaf(LiteralLeaf);
                case InvocationExpressionSyntax invocation when IsNameof(invocation):
                    return Leaf(LiteralLeaf);
                case InvocationExpressionSyntax invocation:
                    return Combine(node, [Normalize(invocation.Expression, true), Normalize(invocation.ArgumentList, false)]);
                case ObjectCreationExpressionSyntax creation:
                    return Combine(node, [Leaf("new " + GetTypeName(creation.Type)), NormalizeOptional(creation.ArgumentList),
                        NormalizeOptional(creation.Initializer)]);
                case SimpleNameSyntax name when isHead:
                    return Leaf("call " + name.Identifier.Text);
                case MemberAccessExpressionSyntax access when isHead:
                    return Combine(node, [Normalize(access.Expression, false), Leaf("call " + access.Name.Identifier.Text)]);
                case MemberBindingExpressionSyntax binding when isHead:
                    return Combine(node, [Leaf("call " + binding.Name.Identifier.Text)]);
                case TypeSyntax type when IsType(type):
                    return Leaf(TypeLeaf);
                case SimpleNameSyntax:
                    return Leaf(SymbolLeaf);
            }

            return Combine(node, GetChildren(node));
        }

        private IEnumerable<ulong?> GetChildren(SyntaxNode node)
        {
            foreach (var child in node.ChildNodesAndTokens())
            {
                if (child.IsNode)
                    yield return Normalize(child.AsNode()!, false);
                else if (child.IsKind(SyntaxKind.IdentifierToken))
                    yield return Leaf(SymbolLeaf);
                else if (IsLiteralToken(child.AsToken()))
                    yield return Leaf(LiteralLeaf);
            }
        }

        private ulong? NormalizeOptional(SyntaxNode? node)
            => node == null ? null : Normalize(node, false);

        private ulong Combine(SyntaxNode node, IEnumerable<ulong?> children)
            => Combine(node.Kind().ToString(), children.Where(child => child.HasValue).Select(child => child!.Value));

        // Внутренний узел: хеш вида и детей по порядку
        private ulong Combine(string label, IEnumerable<ulong> children)
        {
            ulong hash = HashText(label);
            foreach (ulong child in children)
                hash = Mix(hash * 31 + child);

            NodeCount++;
            m_Fingerprints.Add(hash);
            return hash;
        }

        private ulong Leaf(string label)
        {
            ulong hash = HashText(label);
            NodeCount++;
            m_Fingerprints.Add(hash);
            return hash;
        }

        // Тип, а не выражение: объявление, приведение, typeof, аргумент типа, шаблон
        private static bool IsType(TypeSyntax type)
        {
            if (type is PredefinedTypeSyntax or ArrayTypeSyntax or NullableTypeSyntax or TupleTypeSyntax
                or PointerTypeSyntax or RefTypeSyntax or QualifiedNameSyntax or AliasQualifiedNameSyntax)
                return true;

            return SyntaxFacts.IsInTypeOnlyContext(type)
                || type.Parent is TypePatternSyntax or DeclarationPatternSyntax or RecursivePatternSyntax
                    or TypeArgumentListSyntax;
        }

        private static bool IsNameof(InvocationExpressionSyntax invocation)
            => invocation.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" };

        private static bool IsLiteralToken(SyntaxToken token)
        {
            return token.Kind() is SyntaxKind.NumericLiteralToken
                or SyntaxKind.StringLiteralToken
                or SyntaxKind.CharacterLiteralToken
                or SyntaxKind.InterpolatedStringTextToken;
        }

        // Имя типа в new без пространства и аргументов: new Dictionary<int, string>() -> Dictionary
        private static string GetTypeName(TypeSyntax type)
        {
            return type switch
            {
                QualifiedNameSyntax qualified => GetTypeName(qualified.Right),
                AliasQualifiedNameSyntax alias => GetTypeName(alias.Name),
                SimpleNameSyntax simple => simple.Identifier.Text,
                _ => TypeLeaf,
            };
        }

        // SplitMix64: перемешивание битов, чтобы близкие входы давали далекие хеши
        private static ulong Mix(ulong value)
        {
            value += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
