using CSR.Core.Checks;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Checks
{
    // Конец однострочного комментария // (точка) и буквы, которые заменяются (параметр letters)
    public class CommentEndingCheck : SyntaxCheck
    {
        private static readonly Dictionary<string, string> DefaultLetters = new()
        {
            ["ё"] = "е",
            ["Ё"] = "Е",
        };

        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Choice("prefer", "no-period - без точки в конце, period - с точкой, any - не проверять",
                "no-period", "period", "any"),
            CheckParameter.Map("letters", DefaultLetters, "буквы и их замена"),
            CheckParameter.Bool("standaloneOnly", true,
                "точку проверять только у отдельного комментария: без строк-комментариев выше и ниже"),
        ];

        public override string Name
            => "comment-ending";

        public override string Description
            => "точка в конце однострочного комментария, заменяемые буквы";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var prefer = context.Settings.GetString("prefer");
            var letters = context.Settings.GetMap("letters");
            bool standaloneOnly = context.Settings.GetBool("standaloneOnly");
            var comments = Comments.GetLineComments(context.Root, context.Text);
            var fullLines = new HashSet<int>(comments
                .Where(comment => comment.IsFullLine)
                .Select(comment => comment.Line));
            foreach (var comment in comments)
            {
                var text = context.Text.ToString(comment.Span);
                var changes = new List<TextChange>();
                var problems = new List<string>();
                bool isInBlock = comment.IsFullLine
                    && (fullLines.Contains(comment.Line - 1) || fullLines.Contains(comment.Line + 1));
                if (!standaloneOnly || !isInBlock)
                    CheckEnding(comment, text, prefer, changes, problems);

                CheckLetters(comment, text, letters, changes, problems);
                if (problems.Count == 0)
                    continue;

                changes.Sort((a, b) => a.Span.Start.CompareTo(b.Span.Start));
                var replacement = changes.Count == 0 ? null : context.ReplaceInLines(changes);
                context.Report(comment.Span, string.Join("; ", problems), replacement);
            }
        }

        private static void CheckEnding(LineComment comment, string text, string prefer, List<TextChange> changes,
            List<string> problems)
        {
            var trimmed = text.TrimEnd();
            if (comment.Content.Length == 0 || prefer == "any")
                return;

            bool hasPeriod = trimmed.EndsWith(".", StringComparison.Ordinal)
                && !trimmed.EndsWith("..", StringComparison.Ordinal);
            if (prefer == "no-period" && hasPeriod)
            {
                problems.Add("точка в конце комментария");
                changes.Add(new TextChange(new TextSpan(comment.Span.Start + trimmed.Length - 1, 1), ""));
                return;
            }

            var last = trimmed[trimmed.Length - 1];
            if (prefer == "period" && !hasPeriod && last != '!' && last != '?' && last != ':')
            {
                problems.Add("нет точки в конце комментария");
                changes.Add(new TextChange(new TextSpan(comment.Span.Start + trimmed.Length, 0), "."));
            }
        }

        private static void CheckLetters(LineComment comment, string text, IReadOnlyDictionary<string, string> letters,
            List<TextChange> changes, List<string> problems)
        {
            foreach (var pair in letters)
            {
                var index = text.IndexOf(pair.Key, StringComparison.Ordinal);
                if (index < 0)
                    continue;

                problems.Add($"'{pair.Key}' -> '{pair.Value}'");
                while (index >= 0)
                {
                    changes.Add(new TextChange(new TextSpan(comment.Span.Start + index, pair.Key.Length), pair.Value));
                    index = text.IndexOf(pair.Key, index + pair.Key.Length, StringComparison.Ordinal);
                }
            }
        }
    }
}
