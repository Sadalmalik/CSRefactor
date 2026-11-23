using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Style.Checks
{
    // Имя bool с отрицанием: слово из forbiddenWords (IsNotActive) или слово с приставкой из negationPrefixes
    // (IsDisabled), кроме ignoreWords. Тип - по объявлению (bool, bool?, Boolean): var не проверяется.
    // Не проверяются override и явные реализации интерфейса - имя задано базой
    public class BoolNameCheck : SyntaxCheck
    {
        private const string Field = "field";
        private const string Property = "property";
        private const string Parameter = "parameter";
        private const string Local = "local";

        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("members", "виды: field, property, parameter, local", Field, Property, Parameter, Local),
            CheckParameter.Strings("forbiddenWords", "запрещенные слова имени (без учета регистра)", "Not", "No", "Non", "Dont"),
            CheckParameter.Strings(
                "negationPrefixes",
                "приставки отрицания: слово имени, которое с нее начинается (Dis - Disabled)"),
            CheckParameter.Strings("ignoreWords", "слова с приставкой из negationPrefixes, которые не отрицание (Display)"),
        ];

        public override string Name
            => "bool-name";

        public override string Description
            => "отрицание в имени bool";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var kinds = context.Settings.GetStrings("members");
            var forbidden = context.Settings.GetStrings("forbiddenWords");
            var prefixes = context.Settings.GetStrings("negationPrefixes");
            var ignored = context.Settings.GetStrings("ignoreWords");
            foreach (var node in context.Root.DescendantNodes())
            {
                foreach (var identifier in GetBoolNames(node, kinds))
                {
                    var words = NameWords.Split(NameWords.StripFieldPrefix(identifier.Text));
                    var negation = NameWords.FindWord(words, forbidden) ?? FindPrefixed(words, prefixes, ignored);
                    if (negation != null)
                        context.Report(identifier.Span, $"bool '{identifier.Text}': отрицание '{negation}' - имя по состоянию");
                }
            }
        }

        // Имена bool, объявленные узлом
        private static IEnumerable<SyntaxToken> GetBoolNames(SyntaxNode node, IReadOnlyList<string> kinds)
        {
            return node switch
            {
                FieldDeclarationSyntax field when kinds.Contains(Field) && IsBool(field.Declaration.Type)
                    => field.Declaration.Variables.Select(variable => variable.Identifier),
                PropertyDeclarationSyntax property when kinds.Contains(Property) && IsBool(property.Type)
                    && !IsInherited(property.Modifiers) && property.ExplicitInterfaceSpecifier == null
                    => [property.Identifier],
                ParameterSyntax parameter when kinds.Contains(Parameter) && parameter.Type != null
                    && IsBool(parameter.Type) && !IsInheritedSignature(parameter)
                    => [parameter.Identifier],
                LocalDeclarationStatementSyntax local when kinds.Contains(Local) && IsBool(local.Declaration.Type)
                    => local.Declaration.Variables.Select(variable => variable.Identifier),
                DeclarationExpressionSyntax declaration when kinds.Contains(Local) && IsBool(declaration.Type)
                    && declaration.Designation is SingleVariableDesignationSyntax designation
                    => [designation.Identifier],
                _ => [],
            };
        }

        // Слово, которое начинается с приставки отрицания и длиннее нее (Disabled, Unlocked), кроме ignoreWords
        private static string? FindPrefixed(
            IReadOnlyList<string> words,
            IReadOnlyList<string> prefixes,
            IReadOnlyList<string> ignored)
        {
            return words.FirstOrDefault(word => prefixes.Any(prefix => word.Length > prefix.Length
                    && word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                && !ignored.Any(ignore => string.Equals(ignore, word, StringComparison.OrdinalIgnoreCase)));
        }

        private static bool IsBool(TypeSyntax type)
        {
            return type switch
            {
                PredefinedTypeSyntax predefined => predefined.Keyword.IsKind(SyntaxKind.BoolKeyword),
                NullableTypeSyntax nullable => IsBool(nullable.ElementType),
                IdentifierNameSyntax identifier => identifier.Identifier.Text == "Boolean",
                QualifiedNameSyntax qualified => qualified.ToString() == "System.Boolean",
                _ => false,
            };
        }

        private static bool IsInherited(SyntaxTokenList modifiers)
            => modifiers.Any(SyntaxKind.OverrideKeyword);

        // Параметр override или явной реализации интерфейса: имя задано базой
        private static bool IsInheritedSignature(ParameterSyntax parameter)
        {
            return parameter.Parent?.Parent switch
            {
                MethodDeclarationSyntax method => IsInherited(method.Modifiers) || method.ExplicitInterfaceSpecifier != null,
                IndexerDeclarationSyntax indexer => IsInherited(indexer.Modifiers)
                    || indexer.ExplicitInterfaceSpecifier != null,
                _ => false,
            };
        }
    }
}
