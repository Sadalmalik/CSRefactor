using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Модификатор private у членов класса и структуры: явно или не писать.
    // Не проверяются: члены интерфейсов, явные реализации интерфейса, статические конструкторы, деструкторы,
    // partial-методы без модификатора доступа
    public class ExplicitPrivateCheck : SyntaxCheck
    {
        private const string Field = "field";
        private const string Method = "method";
        private const string Property = "property";
        private const string Event = "event";
        private const string Constructor = "constructor";
        private const string NestedType = "type";

        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Choice("prefer", "explicit - private пишется, implicit - не пишется",
                "explicit", "implicit"),
            CheckParameter.Strings("members", "виды членов: field, method, property, event, constructor, type",
                Field, Method),
        ];

        public override string Name
            => "explicit-private";

        public override string Description
            => "явный модификатор private";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            bool preferExplicit = context.Settings.GetString("prefer") == "explicit";
            var kinds = context.Settings.GetStrings("members");
            foreach (var member in context.Root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                if (member.Parent is not (ClassDeclarationSyntax or StructDeclarationSyntax or RecordDeclarationSyntax))
                    continue;

                var kind = GetKind(member);
                if (kind == null || !kinds.Contains(kind) || IsExcluded(member))
                    continue;

                if (preferExplicit && !member.Modifiers.Any(IsAccessModifier))
                {
                    var start = GetDeclarationStart(member);
                    var span = new TextSpan(start, 0);
                    context.Report(member.Span, "нет явного 'private'", context.ReplaceInLines(span, "private "));
                    continue;
                }

                var modifier = member.Modifiers.FirstOrDefault(token => token.IsKind(SyntaxKind.PrivateKeyword));
                if (preferExplicit || modifier == default || member.Modifiers.Count(IsAccessModifier) != 1)
                    continue;

                var removed = TextSpan.FromBounds(modifier.SpanStart, modifier.Span.End
                    + modifier.TrailingTrivia.FullSpan.Length);
                context.Report(modifier.Span, "лишний 'private'", context.ReplaceInLines(removed, ""));
            }
        }

        private static string? GetKind(MemberDeclarationSyntax member)
        {
            return member switch
            {
                FieldDeclarationSyntax => Field,
                MethodDeclarationSyntax => Method,
                PropertyDeclarationSyntax or IndexerDeclarationSyntax => Property,
                EventDeclarationSyntax or EventFieldDeclarationSyntax => Event,
                ConstructorDeclarationSyntax => Constructor,
                BaseTypeDeclarationSyntax or DelegateDeclarationSyntax => NestedType,
                _ => null,
            };
        }

        private static bool IsExcluded(MemberDeclarationSyntax member)
        {
            return member switch
            {
                MethodDeclarationSyntax method => method.ExplicitInterfaceSpecifier != null
                    || method.Modifiers.Any(SyntaxKind.PartialKeyword),
                BasePropertyDeclarationSyntax property => property.ExplicitInterfaceSpecifier != null,
                ConstructorDeclarationSyntax constructor => constructor.Modifiers.Any(SyntaxKind.StaticKeyword),
                _ => false,
            };
        }

        private static bool IsAccessModifier(SyntaxToken token)
        {
            return token.Kind() is SyntaxKind.PublicKeyword
                or SyntaxKind.PrivateKeyword
                or SyntaxKind.ProtectedKeyword
                or SyntaxKind.InternalKeyword
                or SyntaxKind.FileKeyword;
        }

        // Начало объявления после атрибутов: первый модификатор или тип
        private static int GetDeclarationStart(MemberDeclarationSyntax member)
        {
            var first = member.DescendantTokens()
                .First(token => !token.Parent!.AncestorsAndSelf().OfType<AttributeListSyntax>().Any());
            return first.SpanStart;
        }
    }
}
