using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Тип локальной и переменной foreach: var или явный тип.
    // prefer var: явный тип с инициализатором -> var. Не проверяются: bool (bool-local-type), числовые типы, object,
    // nullable, без инициализатора, null / default / stackalloc / [] / лямбда, тернарник с веткой, тип которой
    // не виден в коде (null, вызов, поле: ветки разных типов с var не собираются), new другого типа
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
                ConditionalExpressionSyntax conditional => IsTypeShown(declared, conditional),
                BinaryExpressionSyntax coalesce when coalesce.IsKind(SyntaxKind.CoalesceExpression)
                    => !IsNullLike(coalesce.Right),
                InterpolatedStringExpressionSyntax => declared == "string",
                InvocationExpressionSyntax or MemberAccessExpressionSyntax or IdentifierNameSyntax
                    or ElementAccessExpressionSyntax or AwaitExpressionSyntax
                    or ConditionalAccessExpressionSyntax => true,
                _ => false,
            };
        }

        // Тип выражения виден в коде и равен объявленному. Для тернарника - у каждой ветки: ветки неизвестных типов
        // могут быть разными (база и наследник, интерфейс и класс), с var такой код не собирается (CS0173)
        private static bool IsTypeShown(string declared, ExpressionSyntax value)
        {
            return value switch
            {
                ParenthesizedExpressionSyntax parenthesized => IsTypeShown(declared, parenthesized.Expression),
                ConditionalExpressionSyntax conditional => IsTypeShown(declared, conditional.WhenTrue)
                    && IsTypeShown(declared, conditional.WhenFalse),
                ObjectCreationExpressionSyntax creation => creation.Type.WithoutTrivia().ToString() == declared,
                ArrayCreationExpressionSyntax array => array.Type.WithoutTrivia().ToString() == declared,
                CastExpressionSyntax cast => cast.Type.WithoutTrivia().ToString() == declared,
                BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression)
                    => binary.Right.WithoutTrivia().ToString() == declared,
                LiteralExpressionSyntax literal => declared == "string"
                    && literal.IsKind(SyntaxKind.StringLiteralExpression),
                InterpolatedStringExpressionSyntax => declared == "string",
                _ => false,
            };
        }

        private static bool IsReassigned(LocalDeclarationStatementSyntax local, string name)
        {
            var scope = local.Parent;
            if (scope == null)
                return false;

            return scope.DescendantNodes()
                .Where(node => node.SpanStart > local.Span.End)
                .Any(node => GetAssignedName(node) == name);
        }

        // Имя локальной, которой узел присваивает новое значение: x = ..., out x, ref x; null - не присваивание
        private static string? GetAssignedName(SyntaxNode node)
        {
            var target = node switch
            {
                AssignmentExpressionSyntax assignment => assignment.Left,
                ArgumentSyntax argument when !argument.RefKindKeyword.IsKind(SyntaxKind.None)
                    && !argument.RefKindKeyword.IsKind(SyntaxKind.InKeyword) => argument.Expression,
                _ => null,
            };

            return target is IdentifierNameSyntax identifier ? identifier.Identifier.Text : null;
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
