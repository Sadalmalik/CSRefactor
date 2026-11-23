using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style
{
    // Имена типов по синтаксису для масок проверок
    public static class TypeNames
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
    }
}
