using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Metrics
{
    // Цикломатическая сложность по правилам crapper: 1 + if, циклы, catch, ?:, метки case, ветви switch-выражения,
    // &&, ||, ??, ?. Лямбды входят в объемлющую функцию, локальные функции считаются отдельно
    public static class CyclomaticComplexity
    {
        public static int Calculate(SyntaxNode function)
        {
            int decisions = Functions.OwnNodes(function).Count(IsDecision);
            int operators = Functions.OwnTokens(function).Count(IsDecisionToken);
            return 1 + decisions + operators;
        }

        private static bool IsDecision(SyntaxNode node)
        {
            return node is IfStatementSyntax
                or WhileStatementSyntax
                or ForStatementSyntax
                or CommonForEachStatementSyntax
                or DoStatementSyntax
                or CatchClauseSyntax
                or ConditionalExpressionSyntax
                or CaseSwitchLabelSyntax
                or CasePatternSwitchLabelSyntax
                or SwitchExpressionArmSyntax
                or ConditionalAccessExpressionSyntax;
        }

        private static bool IsDecisionToken(SyntaxToken token)
        {
            return token.IsKind(SyntaxKind.AmpersandAmpersandToken)
                || token.IsKind(SyntaxKind.BarBarToken)
                || token.IsKind(SyntaxKind.QuestionQuestionToken);
        }
    }
}
