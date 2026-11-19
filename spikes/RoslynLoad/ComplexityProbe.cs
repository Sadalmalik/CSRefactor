using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Замер: цикломатическая сложность по правилам crapper (Java: if, циклы, catch, ?:, метка switch, && / ||;
// плюс ?? и ?. как в TypeScript), длина метода и вложенность блоков. Только синтаксис, без загрузки проекта.
internal static class ComplexityProbe
{
    public static void Run(string root)
    {
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).ToList();
        var sw = Stopwatch.StartNew();
        var complexities = new List<int>();
        var lengths = new List<int>();
        var nestings = new List<int>();
        var fileLengths = new List<int>();
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: new[] { "UNITY_EDITOR" });

        foreach (var file in files)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), parseOptions);
            var rootNode = tree.GetRoot();
            fileLengths.Add(tree.GetText().Lines.Count);

            foreach (var member in rootNode.DescendantNodes().Where(IsFunction))
            {
                complexities.Add(1 + member.DescendantNodes(n => n == member || !IsFunction(n)).Count(IsDecision)
                    + member.DescendantTokens(n => n == member || !IsFunction(n)).Count(IsDecisionToken));
                var span = member.GetLocation().GetLineSpan();
                lengths.Add(span.EndLinePosition.Line - span.StartLinePosition.Line + 1);
                nestings.Add(MaxNesting(member));
            }
        }

        Console.WriteLine($"files {files.Count}, functions {complexities.Count}, parse+walk {sw.Elapsed.TotalSeconds:F1}s");
        Print("cyclomatic complexity", complexities, 10, 20, 30);
        Print("function lines", lengths, 30, 60, 100);
        Print("max block nesting", nestings, 3, 4, 6);
        Print("file lines", fileLengths, 316, 500, 1000);
    }

    private static bool IsFunction(SyntaxNode node)
        => node is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax;

    private static bool IsDecision(SyntaxNode node)
        => node is IfStatementSyntax or WhileStatementSyntax or ForStatementSyntax or ForEachStatementSyntax
            or DoStatementSyntax or CatchClauseSyntax or ConditionalExpressionSyntax or CaseSwitchLabelSyntax
            or CasePatternSwitchLabelSyntax or SwitchExpressionArmSyntax or ConditionalAccessExpressionSyntax;

    private static bool IsDecisionToken(SyntaxToken token)
        => token.IsKind(SyntaxKind.AmpersandAmpersandToken) || token.IsKind(SyntaxKind.BarBarToken)
            || token.IsKind(SyntaxKind.QuestionQuestionToken);

    private static int MaxNesting(SyntaxNode member)
    {
        var body = member.ChildNodes().OfType<BlockSyntax>().FirstOrDefault();
        if (body == null)
            return 0;

        return body.DescendantNodes(n => !IsFunction(n) || n == member)
            .Where(n => n is StatementSyntax and not BlockSyntax)
            .Select(n => n.Ancestors().TakeWhile(a => a != body).Count(IsNestingStatement))
            .DefaultIfEmpty(0)
            .Max();
    }

    // else и else if — тот же уровень, что и исходный if
    private static bool IsNestingStatement(SyntaxNode node)
        => node is IfStatementSyntax { Parent: not ElseClauseSyntax } or WhileStatementSyntax or ForStatementSyntax
            or ForEachStatementSyntax or DoStatementSyntax or TryStatementSyntax or SwitchStatementSyntax
            or UsingStatementSyntax or LockStatementSyntax;

    private static void Print(string title, List<int> values, int a, int b, int c)
    {
        if (values.Count == 0)
            return;

        values.Sort();
        int P(double q) => values[(int)Math.Min(values.Count - 1, Math.Floor(q * values.Count))];
        Console.WriteLine($"{title}: p50 {P(0.5)}, p90 {P(0.9)}, p99 {P(0.99)}, max {values[^1]}; "
            + $">{a}: {values.Count(v => v > a)}, >{b}: {values.Count(v => v > b)}, >{c}: {values.Count(v => v > c)}");
    }
}
