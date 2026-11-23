using CSR.Core.Arch;
using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Коллекция с именем в единственном числе. Коллекция - массив или тип из collectionTypes по объявлению (var
    // не проверяется); множественное число - последнее слово на 's' (кроме singularEndings) или из pluralWords
    // (List, Dict, Data). Не проверяются override и явные реализации интерфейса
    public class CollectionNameCheck : SyntaxCheck
    {
        private const string Field = "field";
        private const string Property = "property";
        private const string Parameter = "parameter";
        private const string Local = "local";

        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("members", "виды: field, property, parameter, local", Field, Property),
            CheckParameter.Strings(
                "collectionTypes",
                "маски типов-коллекций (без пространства имен)",
                "List<*>",
                "IList<*>",
                "IReadOnlyList<*>",
                "ICollection<*>",
                "IReadOnlyCollection<*>",
                "IEnumerable<*>",
                "HashSet<*>",
                "ISet<*>",
                "IReadOnlySet<*>",
                "SortedSet<*>",
                "Queue<*>",
                "Stack<*>",
                "LinkedList<*>",
                "Dictionary<*>",
                "IDictionary<*>",
                "IReadOnlyDictionary<*>",
                "SortedDictionary<*>",
                "ConcurrentDictionary<*>"),
            CheckParameter.Bool("arrays", true, "проверять массивы"),
            CheckParameter.Strings(
                "pluralWords",
                "последние слова, которые считаются множественным числом или постфиксом коллекции",
                "List",
                "Array",
                "Dict",
                "Dictionary",
                "Set",
                "Map",
                "Queue",
                "Stack",
                "Collection",
                "Lookup",
                "Table",
                "Index",
                "Registry",
                "Cache",
                "Pool",
                "Buffer",
                "History",
                "All",
                "Data",
                "Children",
                "People"),
            CheckParameter.Strings("singularEndings", "окончания на 's' в единственном числе", "ss", "us", "is"),
        ];

        public override string Name
            => "collection-name";

        public override string Description
            => "коллекция с именем в единственном числе";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var kinds = context.Settings.GetStrings("members");
            var collectionTypes = NameMask.Get(context.Settings.GetStrings("collectionTypes"));
            bool checkArrays = context.Settings.GetBool("arrays");
            var pluralWords = context.Settings.GetStrings("pluralWords");
            var singularEndings = context.Settings.GetStrings("singularEndings");
            foreach (var node in context.Root.DescendantNodes())
            {
                var declared = GetDeclared(node, kinds);
                if (declared == null || !IsCollection(declared.Type, collectionTypes, checkArrays))
                    continue;

                foreach (var identifier in declared.Names)
                {
                    var word = NameWords.GetLastWord(identifier.Text);
                    if (!NameWords.IsPlural(word, pluralWords, singularEndings))
                        context.Report(identifier.Span, $"коллекция '{identifier.Text}': имя во множественном числе");
                }
            }
        }

        // Тип и имена, объявленные узлом
        private static Declared? GetDeclared(SyntaxNode node, IReadOnlyList<string> kinds)
        {
            return node switch
            {
                FieldDeclarationSyntax field when kinds.Contains(Field) => Declared.From(field.Declaration),
                PropertyDeclarationSyntax property when kinds.Contains(Property)
                    && !property.Modifiers.Any(SyntaxKind.OverrideKeyword) && property.ExplicitInterfaceSpecifier == null
                    => new Declared(property.Type, [property.Identifier]),
                ParameterSyntax { Type: not null } parameter when kinds.Contains(Parameter)
                    && !IsInheritedSignature(parameter) => new Declared(parameter.Type, [parameter.Identifier]),
                LocalDeclarationStatementSyntax local when kinds.Contains(Local) => Declared.From(local.Declaration),
                _ => null,
            };
        }

        private static bool IsCollection(TypeSyntax type, NameMask collectionTypes, bool checkArrays)
        {
            return type switch
            {
                ArrayTypeSyntax => checkArrays,
                NullableTypeSyntax nullable => IsCollection(nullable.ElementType, collectionTypes, checkArrays),
                QualifiedNameSyntax qualified => IsCollection(qualified.Right, collectionTypes, checkArrays),
                GenericNameSyntax generic => collectionTypes.IsMatch(generic.WithoutTrivia().ToString()),
                _ => false,
            };
        }

        private static bool IsInheritedSignature(ParameterSyntax parameter)
        {
            return parameter.Parent?.Parent is MethodDeclarationSyntax method
                && (method.Modifiers.Any(SyntaxKind.OverrideKeyword) || method.ExplicitInterfaceSpecifier != null);
        }

        // Тип объявления и его имена
        private class Declared
        {
            public Declared(TypeSyntax type, IReadOnlyList<SyntaxToken> names)
            {
                Type = type;
                Names = names;
            }

            public TypeSyntax Type { get; }

            public IReadOnlyList<SyntaxToken> Names { get; }

            public static Declared From(VariableDeclarationSyntax declaration)
                => new(declaration.Type, declaration.Variables.Select(variable => variable.Identifier).ToList());
        }
    }
}
