using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Тип локальной и переменной foreach: var или явный тип.
    // prefer var: явный тип с инициализатором -> var. Не проверяются: bool (bool-local-type), числовые типы, object,
    // nullable, без инициализатора, null / default / stackalloc / [] / лямбда, тернарник с null, new другого типа
    // (объявлен базовый тип), переменная, которой дальше присваивается новое значение (тип мог быть выбран под него),
    // const, несколько переменных; foreach - кроме типов из foreachExplicitTypes
    // (необобщенный IEnumerable без семантики не отличить).
    // prefer explicit: var -> находка без замены, кроме инициализатора, где тип виден (new T, приведение, as)
    public class LocalTypeCheck : SyntaxCheck
    {
        private static readonly HashSet<SyntaxKind> SkippedPredefined =
        [
            SyntaxKind.BoolKeyword,
            SyntaxKind.ObjectKeyword,
            SyntaxKind.ByteKeyword,
            SyntaxKind.SByteKeyword,
            SyntaxKind.ShortKeyword,
            SyntaxKind.UShortKeyword,
            SyntaxKind.IntKeyword,
            SyntaxKind.UIntKeyword,
            SyntaxKind.LongKeyword,
            SyntaxKind.ULongKeyword,
            SyntaxKind.FloatKeyword,
            SyntaxKind.DoubleKeyword,
            SyntaxKind.DecimalKeyword,
            SyntaxKind.CharKeyword,
        ];

        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Choice(
                "prefer",
                "var - var там, где тип виден справа или не важен; explicit - явный тип",
                "var",
                "explicit"),
            CheckParameter.Bool("foreach", true, "проверять переменную foreach"),
            CheckParameter.Strings(
                "foreachExplicitTypes",
                "типы элементов, у которых в foreach остается явный тип (необобщенный IEnumerable, например Transform)"),
        ];

        public override string Name
            => "local-type";

        public override string Description
            => "var или явный тип у локальной";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            bool preferVar = context.Settings.GetString("prefer") == "var";
            bool checkForeach = context.Settings.GetBool("foreach");
            var foreachExplicit = context.Settings.GetStrings("foreachExplicitTypes");
            foreach (var node in context.Root.DescendantNodes())
            {
                if (node is LocalDeclarationStatementSyntax local)
                    CheckLocal(context, local, preferVar);
                else if (checkForeach && preferVar && node is ForEachStatementSyntax loop)
                    CheckForeach(context, loop, foreachExplicit);
            }
        }

        private static void CheckLocal(CheckContext context, LocalDeclarationStatementSyntax local, bool preferVar)
        {
            var declaration = local.Declaration;
            if (local.IsConst || declaration.Variables.Count != 1)
                return;

            var variable = declaration.Variables[0];
            var value = variable.Initializer?.Value;
            if (value == null)
                return;

            var type = declaration.Type;
            var name = variable.Identifier.Text;
            if (!preferVar)
            {
                if (type.IsVar && !IsTypeVisible(value))
                    context.Report(type.Span, $"'var {name}' - тип явно");
                return;
            }

            if (type.IsVar || IsSkippedType(type) || !IsVarCompatible(type, value) || IsReassigned(local, name))
                return;

            var changes = new List<TextChange> { new(type.Span, "var") };
            if (value is ImplicitObjectCreationExpressionSyntax creation)
                changes.Add(new TextChange(creation.NewKeyword.Span, "new " + type.WithoutTrivia()));

            var replacement = context.ReplaceInLines(changes);
            context.Report(type.Span, $"'{type} {name}' -> 'var {name}'", replacement);
        }

        private static void CheckForeach(
            CheckContext context,
            ForEachStatementSyntax loop,
            IReadOnlyList<string> exceptions)
        {
            var type = loop.Type;
            if (type.IsVar || IsSkippedType(type) || exceptions.Contains(type.WithoutTrivia().ToString()))
                return;

            var name = loop.Identifier.Text;
            var replacement = context.ReplaceInLines(type.Span, "var");
            context.Report(type.Span, $"'foreach ({type} {name}' -> 'foreach (var {name}'", replacement);
        }

        private static bool IsSkippedType(TypeSyntax type)
        {
            return type switch
            {
                PredefinedTypeSyntax predefined => SkippedPredefined.Contains(predefined.Keyword.Kind()),
                NullableTypeSyntax or TupleTypeSyntax or PointerTypeSyntax or RefTypeSyntax => true,
                _ => false,
            };
        }

        // Вывод var дает тот же тип: инициализатор не требует целевого типа и не создает другой тип
        private static bool IsVarCompatible(TypeSyntax type, ExpressionSyntax value)
        {
            var declared = type.WithoutTrivia().ToString();
            return value switch
            {
                ParenthesizedExpressionSyntax parenthesized => IsVarCompatible(type, parenthesized.Expression),
                LiteralExpressionSyntax literal => literal.IsKind(SyntaxKind.StringLiteralExpression),
                ObjectCreationExpressionSyntax creation => creation.Type.WithoutTrivia().ToString() == declared,
                ImplicitObjectCreationExpressionSyntax => true,
                ArrayCreationExpressionSyntax array => array.Type.WithoutTrivia().ToString() == declared,
                CastExpressionSyntax cast => cast.Type.WithoutTrivia().ToString() == declared,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression)
                    => binary.Right.WithoutTrivia().ToString() == declared,
                ConditionalExpressionSyntax conditional => !IsNullLike(conditional.WhenTrue)
                    && !IsNullLike(conditional.WhenFalse)
                    && IsVarCompatible(type, conditional.WhenTrue)
                    && IsVarCompatible(type, conditional.WhenFalse),
                BinaryExpressionSyntax coalesce when coalesce.IsKind(SyntaxKind.CoalesceExpression)
                    => !IsNullLike(coalesce.Right),
                InterpolatedStringExpressionSyntax => declared == "string",
                InvocationExpressionSyntax or MemberAccessExpressionSyntax or IdentifierNameSyntax
                    or ElementAccessExpressionSyntax or AwaitExpressionSyntax
                    or ConditionalAccessExpressionSyntax => true,
                _ => false,
            };
        }

        private static bool IsReassigned(LocalDeclarationStatementSyntax local, string name)
        {
            var scope = local.Parent;
            if (scope == null)
                return false;

            return scope.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Any(assignment => assignment.SpanStart > local.Span.End
                    && assignment.Left is IdentifierNameSyntax { Identifier.Text: var target }
                    && target == name);
        }

        private static bool IsNullLike(ExpressionSyntax expression)
        {
            return expression.IsKind(SyntaxKind.NullLiteralExpression)
                || expression.IsKind(SyntaxKind.DefaultLiteralExpression)
                || expression is DefaultExpressionSyntax;
        }

        private static bool IsTypeVisible(ExpressionSyntax value)
        {
            return value is ObjectCreationExpressionSyntax or ArrayCreationExpressionSyntax or CastExpressionSyntax
                || value.IsKind(SyntaxKind.AsExpression);
        }
    }
}
