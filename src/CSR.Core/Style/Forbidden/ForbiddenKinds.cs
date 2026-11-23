using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Forbidden
{
    // Обращение кода к имени, которое может быть запрещено: место и имя для масок
    public class ForbiddenUse
    {
        public ForbiddenUse(TextSpan span, string name)
        {
            Span = span;
            Name = name;
        }

        public TextSpan Span { get; }

        public string Name { get; }
    }

    // Виды записей forbidden: какой синтаксис сравнивается с маской и как начинается сообщение
    public static class ForbiddenKinds
    {
        public const string Call = "call";
        public const string Type = "type";

        public static readonly IReadOnlyList<string> All = [Call, Type];

        public static string GetSubject(string kind)
            => kind == Call ? "запрещенный член" : "запрещенный тип";

        public static IEnumerable<ForbiddenUse> Find(string kind, SyntaxNode root)
            => kind == Call ? FindMembers(root) : FindTypes(root);

        // Обращение Тип.Член: последний идентификатор слева от точки и член (UnityEngine.Debug.Log -> Debug.Log,
        // Service<T>.Instance -> Service.Instance, string.Format -> string.Format)
        private static IEnumerable<ForbiddenUse> FindMembers(SyntaxNode root)
        {
            foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            {
                var owner = GetOwnerName(access.Expression);
                if (owner != null)
                    yield return new ForbiddenUse(access.Span, $"{owner}.{access.Name.Identifier.Text}");
            }
        }

        // Имя в позиции типа: объявление, параметр, результат, аргумент типа, new, приведение, typeof. Правая часть
        // без пространства имен с аргументами типа; встроенный тип - ключевым словом; массив - по типу элемента
        private static IEnumerable<ForbiddenUse> FindTypes(SyntaxNode root)
        {
            foreach (var type in root.DescendantNodes().OfType<TypeSyntax>())
            {
                if (type is not (NameSyntax or PredefinedTypeSyntax)
                    || type.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax)
                    continue;

                if (SyntaxFacts.IsInTypeOnlyContext(type))
                    yield return new ForbiddenUse(type.Span, TypeNames.GetRightName(type));
            }
        }

        private static string? GetOwnerName(ExpressionSyntax expression)
        {
            return expression switch
            {
                SimpleNameSyntax simple => simple.Identifier.Text,
                PredefinedTypeSyntax predefined => predefined.Keyword.Text,
                MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
                AliasQualifiedNameSyntax alias => alias.Name.Identifier.Text,
                _ => null,
            };
        }
    }
}
