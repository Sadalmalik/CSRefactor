using System.Text.RegularExpressions;
using CSR.Core.Checks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Закомментированный код: комментарий //, который разбирается как операторы или члены C#.
    // Кандидат - строка с признаком кода (; { } в конце, ключевое слово оператора в начале);
    // подряд идущие строки, которые вместе разбираются как код, - одна находка
    public class CommentedCodeCheck : SyntaxCheck
    {
        private static readonly Regex StatementStart = new(
            @"^(if|else|for|foreach|while|do|switch|case|return|var|using|break|continue|throw|yield)\b",
            RegexOptions.CultureInvariant);

        private static readonly Regex Todo = new(
            @"^(todo|fixme|hack)\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public override string Name
            => "commented-code";

        public override string Description
            => "закомментированный код";

        public override void Analyze(CheckContext context)
        {
            var comments = Comments.GetLineComments(context.Root, context.Text);
            var run = new List<LineComment>();
            foreach (var comment in comments)
            {
                if (!comment.IsFullLine)
                {
                    CheckTrailing(context, comment);
                    continue;
                }

                if (run.Count > 0 && run[run.Count - 1].Line + 1 != comment.Line)
                {
                    CheckRun(context, run);
                    run.Clear();
                }

                run.Add(comment);
            }

            CheckRun(context, run);
        }

        private static void CheckRun(CheckContext context, List<LineComment> run)
        {
            var candidates = run.Where(comment => IsCandidate(comment.Content) && IsCode(comment.Content)).ToList();
            if (candidates.Count == 0)
                return;

            if (run.Count > 1 && IsCode(string.Join("\n", run.Select(comment => comment.Content))))
            {
                var span = TextSpan.FromBounds(run[0].Span.Start, run[run.Count - 1].Span.End);
                context.Report(span, "закомментированный код", context.ReplaceLines(span, []));
                return;
            }

            foreach (var comment in candidates)
                context.Report(comment.Span, "закомментированный код", context.ReplaceLines(comment.Span, []));
        }

        // Хвост после кода: result += step; // * weight;
        private static void CheckTrailing(CheckContext context, LineComment comment)
        {
            var content = comment.Content;
            if (!content.EndsWith(";", StringComparison.Ordinal))
                return;

            if (!IsCode(content) && !IsCode("_ " + content))
                return;

            var span = Comments.GetTrailingSpan(comment, context.Text);
            context.Report(comment.Span, "закомментированный код в конце строки", context.ReplaceInLines(span, ""));
        }

        private static bool IsCandidate(string content)
        {
            if (content.Length == 0 || Todo.IsMatch(content))
                return false;

            return content.EndsWith(";", StringComparison.Ordinal)
                || content.EndsWith("{", StringComparison.Ordinal)
                || content.EndsWith("}", StringComparison.Ordinal)
                || StatementStart.IsMatch(content);
        }

        private static bool IsCode(string text)
        {
            var block = SyntaxFactory.ParseStatement("{\n" + text + "\n}");
            if (!block.ContainsDiagnostics && block.FullSpan.Length == text.Length + 4)
                return true;

            var unit = SyntaxFactory.ParseCompilationUnit("class C\n{\n" + text + "\n}");
            return !unit.ContainsDiagnostics;
        }
    }
}
