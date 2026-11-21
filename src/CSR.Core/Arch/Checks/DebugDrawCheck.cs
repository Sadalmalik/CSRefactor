using CSR.Core.Style;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // Отладочная отрисовка мимо механизма проекта: вызов из drawMethods в логике (не в члене из debugMethods)
    // с длительностью или без проверки флага (if, ?:, &&, guard if (...) return выше в блоке);
    // bool-флаг отладки (flagNames) полем или свойством типа, имя которого не из settingsTypes
    public class DebugDrawCheck : ArchCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Strings("drawMethods", "маски вызовов отрисовки (текст вызова без пробелов)",
                "Debug.DrawLine", "Debug.DrawRay"),
            CheckParameter.Int("durationArgument", 4, "номер аргумента длительности (с 1); 0 - не проверять"),
            CheckParameter.Strings("debugMethods", "маски членов отладочной отрисовки, где вызовы не ищутся",
                "OnDrawGizmos*", "Draw*", "*Debug*", "*Gizmo*"),
            CheckParameter.Strings("flagNames", "маски имен bool-флагов отладки (без префикса m_, _)",
                "*Debug*", "Draw*"),
            CheckParameter.Strings("settingsTypes", "маски имен типов настроек, где флаги отладки - норма",
                "*Settings", "*Config", "*Options"),
        ];

        public override string Name
            => "debug-draw";

        public override string Description
            => "отладочная отрисовка мимо механизма проекта";

        public override string Question
            => "Отрисовать механизмом отрисовки проекта под флагом из настроек системы, "
                + "отдельным методом или регионом Debug?";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        protected override void AnalyzeFile(CheckContext context)
        {
            var drawMethods = NameMask.Get(context.Settings.GetStrings("drawMethods"));
            foreach (var invocation in context.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (drawMethods.IsMatch(Invocations.GetTargetText(invocation)))
                    CheckDraw(context, invocation);
            }

            foreach (var type in context.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                CheckFlags(context, type);
        }

        private static void CheckDraw(CheckContext context, InvocationExpressionSyntax invocation)
        {
            var member = ArchSyntax.GetEnclosingMemberName(invocation);
            if (member != null && NameMask.Get(context.Settings.GetStrings("debugMethods")).IsMatch(member))
                return;

            var reasons = new List<string>();
            if (HasDuration(invocation, context.Settings.GetInt("durationArgument")))
                reasons.Add("с длительностью");

            if (!IsUnderCondition(invocation))
                reasons.Add("без флага");

            if (reasons.Count == 0)
                return;

            var where = member == null ? "" : $" в '{member}'";
            var message = $"{Invocations.GetTargetText(invocation)} {string.Join(" и ", reasons)}{where}";
            context.Report(invocation.Span, message);
        }

        private static bool HasDuration(InvocationExpressionSyntax invocation, int position)
        {
            if (position <= 0)
                return false;

            var arguments = invocation.ArgumentList.Arguments;
            return arguments.Count >= position
                || arguments.Any(argument => argument.NameColon?.Name.Identifier.Text == "duration");
        }

        // Вызов под условием: внутри if, ?: или правой части &&, либо выше в блоке есть guard if (...) return
        private static bool IsUnderCondition(SyntaxNode node)
        {
            foreach (var ancestor in node.Ancestors())
            {
                switch (ancestor)
                {
                    case IfStatementSyntax or ConditionalExpressionSyntax:
                        return true;
                    case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression):
                        return true;
                    case BlockSyntax block when HasGuardBefore(block, node):
                        return true;
                    case MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax:
                        return false;
                }
            }

            return false;
        }

        private static bool HasGuardBefore(BlockSyntax block, SyntaxNode node)
        {
            return block.Statements
                .TakeWhile(statement => statement.SpanStart < node.SpanStart && !statement.Span.Contains(node.Span))
                .OfType<IfStatementSyntax>()
                .Any(statement => IsReturn(statement.Statement));
        }

        private static bool IsReturn(StatementSyntax statement)
        {
            if (statement is BlockSyntax { Statements.Count: 1 } block)
                return block.Statements[0] is ReturnStatementSyntax;

            return statement is ReturnStatementSyntax;
        }

        private static void CheckFlags(CheckContext context, TypeDeclarationSyntax type)
        {
            if (NameMask.Get(context.Settings.GetStrings("settingsTypes")).IsMatch(type.Identifier.Text))
                return;

            var flags = NameMask.Get(context.Settings.GetStrings("flagNames"), true);
            foreach (var member in type.Members)
            {
                foreach (var identifier in GetBoolMembers(member))
                {
                    var name = string.Concat(ArchSyntax.SplitWords(identifier.Text));
                    var message = $"флаг отладки '{identifier.Text}' полем '{type.Identifier.Text}'";
                    if (flags.IsMatch(name))
                        context.Report(identifier.Span, message);
                }
            }
        }

        private static IEnumerable<SyntaxToken> GetBoolMembers(MemberDeclarationSyntax member)
        {
            if (member.Modifiers.Any(SyntaxKind.ConstKeyword))
                return Array.Empty<SyntaxToken>();

            return member switch
            {
                FieldDeclarationSyntax field when IsBool(field.Declaration.Type)
                    => field.Declaration.Variables.Select(variable => variable.Identifier),
                PropertyDeclarationSyntax property when IsBool(property.Type) => [property.Identifier],
                _ => Array.Empty<SyntaxToken>(),
            };
        }

        private static bool IsBool(TypeSyntax type)
            => type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.BoolKeyword);
    }
}
