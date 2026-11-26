using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSR.Core.Checks;

namespace CSR.Core.Syntax
{
    // Общие разборы синтаксиса для проверок style и arch: видимость, базовые типы, атрибуты, охватывающий член
    public static class SyntaxTools
    {
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

            return type.BaseList.Types.Any(baseType => baseTypes.IsMatch(TypeNameTools.GetSimpleTypeName(baseType.Type)));
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

        // У члена есть атрибут с одним из имен (без суффикса Attribute)
        public static bool HasAttribute(MemberDeclarationSyntax member, IReadOnlyList<string> names)
        {
            return member.AttributeLists
                .SelectMany(list => list.Attributes)
                .Any(attribute => names.Contains(TypeNameTools.GetAttributeName(attribute.Name)));
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
