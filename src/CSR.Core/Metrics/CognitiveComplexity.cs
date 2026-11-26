using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Metrics
{
    // Когнитивная сложность по спецификации SonarSource (G. Ann Campbell, 2017):
    // +1 и надбавка за вложенность - if, ?:, switch, switch-выражение, циклы, catch;
    // +1 без надбавки - else if, else, goto, рекурсивный вызов,
    // каждая последовательность одинаковых && / || / and / or;
    // вложенность растет в телах этих конструкций и в лямбдах. ?? и ?. не считаются
    // Отличия от спецификации: рекурсия - по имени и числу аргументов (без семантики), только прямая;
    // локальные функции считаются отдельно, как и в цикломатической сложности
    public class CognitiveComplexity : CSharpSyntaxWalker
    {
        private readonly SyntaxNode m_Function;
        private readonly string? m_RecursionName;
        private readonly int m_MinArguments;
        private readonly int m_MaxArguments;
        private int m_Nesting;
        private int m_Complexity;

        private CognitiveComplexity(SyntaxNode function)
        {
            m_Function = function;
            m_RecursionName = function switch
            {
                MethodDeclarationSyntax method => method.Identifier.Text,
                LocalFunctionStatementSyntax local => local.Identifier.Text,
                _ => null,
            };

            BaseParameterListSyntax? parameters = FunctionTools.GetParameters(function);
            if (parameters == null)
                return;

            m_MaxArguments = parameters.Parameters.Any(IsParams) ? int.MaxValue : parameters.Parameters.Count;
            m_MinArguments = parameters.Parameters.Count(
                parameter => parameter.Default == null && !IsParams(parameter));
        }

        public static int Calculate(SyntaxNode function)
        {
            var walker = new CognitiveComplexity(function);
            walker.Visit(function);
            return walker.m_Complexity;
        }

#region Visitors

        public override void Visit(SyntaxNode? node)
        {
            if (node != m_Function && node is LocalFunctionStatementSyntax)
                return;

            base.Visit(node);
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            if (node.Parent is ElseClauseSyntax)
                m_Complexity++;
            else
                IncreaseWithNesting();

            Visit(node.Condition);
            VisitNested(node.Statement);

            if (node.Else == null)
                return;

            if (node.Else.Statement is IfStatementSyntax)
            {
                Visit(node.Else.Statement);
                return;
            }

            m_Complexity++;
            VisitNested(node.Else.Statement);
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            IncreaseWithNesting();
            Visit(node.Condition);
            VisitNested(node.WhenTrue);
            VisitNested(node.WhenFalse);
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            IncreaseWithNesting();
            Visit(node.Expression);
            foreach (var section in node.Sections)
                VisitNested(section);
        }

        public override void VisitSwitchExpression(SwitchExpressionSyntax node)
        {
            IncreaseWithNesting();
            Visit(node.GoverningExpression);
            foreach (var arm in node.Arms)
                VisitNested(arm);
        }

        public override void VisitForStatement(ForStatementSyntax node)
            => VisitLoop(node, node.Statement);

        public override void VisitForEachStatement(ForEachStatementSyntax node)
            => VisitLoop(node, node.Statement);

        public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
            => VisitLoop(node, node.Statement);

        public override void VisitWhileStatement(WhileStatementSyntax node)
            => VisitLoop(node, node.Statement);

        public override void VisitDoStatement(DoStatementSyntax node)
            => VisitLoop(node, node.Statement);

        public override void VisitCatchClause(CatchClauseSyntax node)
        {
            IncreaseWithNesting();
            Visit(node.Declaration);
            Visit(node.Filter);
            VisitNested(node.Block);
        }

        public override void VisitGotoStatement(GotoStatementSyntax node)
        {
            m_Complexity++;
            base.VisitGotoStatement(node);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.LogicalAndExpression) || node.IsKind(SyntaxKind.LogicalOrExpression))
                CountSequence(node);

            base.VisitBinaryExpression(node);
        }

        public override void VisitBinaryPattern(BinaryPatternSyntax node)
        {
            CountSequence(node);
            base.VisitBinaryPattern(node);
        }

        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
            => VisitNested(node, base.VisitSimpleLambdaExpression);

        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
            => VisitNested(node, base.VisitParenthesizedLambdaExpression);

        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
            => VisitNested(node, base.VisitAnonymousMethodExpression);

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (IsRecursiveCall(node))
                m_Complexity++;

            base.VisitInvocationExpression(node);
        }

#endregion


#region Helpers

        private void IncreaseWithNesting()
            => m_Complexity += 1 + m_Nesting;

        private void VisitNested(SyntaxNode? node)
        {
            m_Nesting++;
            Visit(node);
            m_Nesting--;
        }

        private void VisitNested<T>(T node, Action<T> visit)
        {
            m_Nesting++;
            visit(node);
            m_Nesting--;
        }

        private void VisitLoop(StatementSyntax loop, StatementSyntax body)
        {
            IncreaseWithNesting();
            foreach (var child in loop.ChildNodes())
            {
                if (child == body)
                    VisitNested(child);
                else
                    Visit(child);
            }
        }

        // Последовательность одинаковых операторов - одна надбавка:
        // оператор, продолжающий родителя того же вида, не считается
        private void CountSequence(SyntaxNode node)
        {
            SyntaxNode? parent = node.Parent;
            while (parent is ParenthesizedExpressionSyntax or ParenthesizedPatternSyntax)
                parent = parent.Parent;

            if (parent == null || parent.RawKind != node.RawKind)
                m_Complexity++;
        }

        private bool IsRecursiveCall(InvocationExpressionSyntax node)
        {
            if (m_RecursionName == null)
                return false;

            SimpleNameSyntax? name = node.Expression switch
            {
                SimpleNameSyntax simple => simple,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => access.Name,
                _ => null,
            };

            if (name == null || name.Identifier.Text != m_RecursionName)
                return false;

            int arguments = node.ArgumentList.Arguments.Count;
            return arguments >= m_MinArguments && arguments <= m_MaxArguments;
        }

        private static bool IsParams(ParameterSyntax parameter)
            => parameter.Modifiers.Any(SyntaxKind.ParamsKeyword);

#endregion
    }
}
