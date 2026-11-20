using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Metrics
{
    // Члены типа по видам. Поля и события-поля считаются по переменным: int a, b; - два поля
    public static class TypeMembers
    {
        public const string Constants = "constants";
        public const string Fields = "fields";
        public const string Properties = "properties";
        public const string Indexers = "indexers";
        public const string Events = "events";
        public const string Constructors = "constructors";
        public const string Destructors = "destructors";
        public const string Methods = "methods";
        public const string Operators = "operators";
        public const string NestedTypes = "types";
        public const string EnumValues = "values";

        public static string GetKind(BaseTypeDeclarationSyntax type)
        {
            return type switch
            {
                RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword)
                    => "record struct",
                RecordDeclarationSyntax => "record",
                ClassDeclarationSyntax => "class",
                StructDeclarationSyntax => "struct",
                InterfaceDeclarationSyntax => "interface",
                EnumDeclarationSyntax => "enum",
                _ => "type",
            };
        }

        public static string GetName(BaseTypeDeclarationSyntax type)
        {
            if (type is TypeDeclarationSyntax { TypeParameterList: { } typeParameters })
                return type.Identifier.Text + typeParameters;

            return type.Identifier.Text;
        }

        public static Dictionary<string, int> Count(BaseTypeDeclarationSyntax type)
        {
            var counts = new Dictionary<string, int>();
            if (type is EnumDeclarationSyntax enumDeclaration)
            {
                Add(counts, EnumValues, enumDeclaration.Members.Count);
                return counts;
            }

            if (type is not TypeDeclarationSyntax declaration)
                return counts;

            foreach (MemberDeclarationSyntax member in declaration.Members)
                Add(counts, GetMemberKind(member), GetMemberCount(member));

            return counts;
        }

        private static string GetMemberKind(MemberDeclarationSyntax member)
        {
            return member switch
            {
                FieldDeclarationSyntax field when field.Modifiers.Any(SyntaxKind.ConstKeyword) => Constants,
                FieldDeclarationSyntax => Fields,
                PropertyDeclarationSyntax => Properties,
                IndexerDeclarationSyntax => Indexers,
                EventDeclarationSyntax or EventFieldDeclarationSyntax => Events,
                ConstructorDeclarationSyntax => Constructors,
                DestructorDeclarationSyntax => Destructors,
                MethodDeclarationSyntax => Methods,
                OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax => Operators,
                BaseTypeDeclarationSyntax or DelegateDeclarationSyntax => NestedTypes,
                _ => "other",
            };
        }

        private static int GetMemberCount(MemberDeclarationSyntax member)
            => member is BaseFieldDeclarationSyntax field ? field.Declaration.Variables.Count : 1;

        private static void Add(Dictionary<string, int> counts, string kind, int count)
        {
            counts.TryGetValue(kind, out int current);
            counts[kind] = current + count;
        }
    }
}
