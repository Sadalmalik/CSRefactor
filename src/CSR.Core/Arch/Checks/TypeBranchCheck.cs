using CSR.Core.Checks;
using CSR.Core.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Arch.Checks
{
    // Ветвление по конкретному типу: x is T t, x is T { }, case T t:, T t => в switch, x is T, x as T,
    // x.GetType() == typeof(T). switch по типам - одна находка со списком типов.
    // Не ищется: встроенные типы, параметры типа, интерфейсы по имени (I и заглавная), x is A.B без переменной
    // (не отличить от значения enum), case T: без переменной, OfType<T>()
    public class TypeBranchCheck : ArchCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Bool("ignoreInterfaces", true, "не искать проверки на интерфейс (имя I и заглавная буква)"),
            CheckParameter.Strings("ignoreTypes", "маски имен типов, проверки на которые не ищутся"),
            CheckParameter.Bool("asCast", true, "искать приведение x as T"),
            CheckParameter.Strings("ignoreMethods", "маски имен методов, где проверки типа не ищутся", "Equals"),
        ];

        public override string Name
            => "type-branch";

        public override string Description
            => "ветвление по конкретному типу";

        public override string Question
            => "Может ли тип сам отдать нужное через метод базы или интерфейса, чтобы этот код не знал частный случай?";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var groups = new Dictionary<SyntaxNode, List<string>>();
            foreach (var node in context.Root.DescendantNodes())
            {
                var type = GetCheckedType(node, context.Settings.GetBool("asCast"));
                if (type == null || !IsReported(context, node, type))
                    continue;

                var group = GetSwitch(node) ?? node;
                if (!groups.TryGetValue(group, out List<string>? types))
                {
                    types = new List<string>();
                    groups[group] = types;
                }

                var name = SyntaxParts.GetTypeText(type);
                if (!types.Contains(name))
                    types.Add(name);
            }

            foreach (var pair in groups.OrderBy(pair => pair.Key.SpanStart))
                context.Report(GetReportSpan(pair.Key), GetMessage(pair.Key, pair.Value));
        }

        // Тип, на который проверяется значение; null - узел не проверка типа
        private static TypeSyntax? GetCheckedType(SyntaxNode node, bool isAsIncluded)
        {
            return node switch
            {
                DeclarationPatternSyntax declaration => declaration.Type,
                RecursivePatternSyntax recursive => recursive.Type,
                TypePatternSyntax pattern => pattern.Type,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.IsExpression) => GetIsType(binary),
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression) && isAsIncluded
                    => binary.Right as TypeSyntax,
                BinaryExpressionSyntax binary => GetTypeOfComparison(binary),
                _ => null,
            };
        }

        // x is T: только простое имя; A.B может быть значением enum
        private static TypeSyntax? GetIsType(BinaryExpressionSyntax binary)
            => binary.Right is SimpleNameSyntax name ? name : null;

        // x.GetType() == typeof(T) в любом порядке
        private static TypeSyntax? GetTypeOfComparison(BinaryExpressionSyntax binary)
        {
            if (!binary.IsKind(SyntaxKind.EqualsExpression) && !binary.IsKind(SyntaxKind.NotEqualsExpression))
                return null;

            var typeOf = binary.Left as TypeOfExpressionSyntax ?? binary.Right as TypeOfExpressionSyntax;
            var other = typeOf == binary.Left ? binary.Right : binary.Left;
            return typeOf != null && IsGetTypeCall(other) ? typeOf.Type : null;
        }

        private static bool IsGetTypeCall(ExpressionSyntax expression)
        {
            if (expression is not InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } invocation)
                return false;

            return invocation.Expression switch
            {
                MemberAccessExpressionSyntax access => access.Name.Identifier.Text == "GetType",
                IdentifierNameSyntax name => name.Identifier.Text == "GetType",
                _ => false,
            };
        }

        private static bool IsReported(CheckContext context, SyntaxNode node, TypeSyntax type)
        {
            if (type is PredefinedTypeSyntax or NullableTypeSyntax { ElementType: PredefinedTypeSyntax })
                return false;

            var name = SyntaxParts.GetSimpleTypeName(type);
            if (context.Settings.GetBool("ignoreInterfaces") && SyntaxParts.IsInterfaceName(name))
                return false;

            if (IsTypeParameter(node, name))
                return false;

            if (NameMask.Get(context.Settings.GetStrings("ignoreTypes")).IsMatch(name))
                return false;

            var member = SyntaxParts.GetEnclosingMemberName(node);
            return member == null || !NameMask.Get(context.Settings.GetStrings("ignoreMethods")).IsMatch(member);
        }

        // Параметр типа метода, локальной функции или типа, где стоит проверка: x as T
        private static bool IsTypeParameter(SyntaxNode node, string name)
        {
            foreach (var ancestor in node.Ancestors())
            {
                var parameters = ancestor switch
                {
                    MethodDeclarationSyntax method => method.TypeParameterList,
                    LocalFunctionStatementSyntax function => function.TypeParameterList,
                    TypeDeclarationSyntax type => type.TypeParameterList,
                    _ => null,
                };

                if (parameters != null && parameters.Parameters.Any(parameter => parameter.Identifier.Text == name))
                    return true;
            }

            return false;
        }

        // switch, в метке или ветке которого стоит проверка типа
        private static SyntaxNode? GetSwitch(SyntaxNode node)
        {
            foreach (var ancestor in node.Ancestors())
            {
                switch (ancestor)
                {
                    case CasePatternSwitchLabelSyntax label:
                        return label.Parent?.Parent;
                    case SwitchExpressionArmSyntax arm when arm.Pattern.Span.Contains(node.Span):
                        return arm.Parent;
                    case StatementSyntax or MemberDeclarationSyntax:
                        return null;
                }
            }

            return null;
        }

        private static TextSpan GetReportSpan(SyntaxNode node)
        {
            return node switch
            {
                SwitchStatementSyntax statement => statement.SwitchKeyword.Span,
                SwitchExpressionSyntax expression => expression.SwitchKeyword.Span,
                _ => node.Span,
            };
        }

        private static string GetMessage(SyntaxNode node, List<string> types)
        {
            if (node is SwitchStatementSyntax or SwitchExpressionSyntax)
                return $"switch по типам: {SyntaxParts.JoinNames(types)}";

            var pattern = node.FirstAncestorOrSelf<IsPatternExpressionSyntax>();
            var subject = node is BinaryExpressionSyntax binary ? binary.Left : pattern?.Expression;
            if (subject is ThisExpressionSyntax)
                return $"проверка this на тип '{types[0]}': базовый класс знает наследника?";

            return $"проверка на тип '{types[0]}': {SyntaxParts.Shorten(pattern ?? node)}";
        }
    }
}
