using System.Text.RegularExpressions;
using CSR.Core.Checks;

namespace CSR.Core.Style.Checks
{
    // Формат TODO: // TODO: <что сделать>, без автора, даты и номера задачи
    public class TodoFormatCheck : SyntaxCheck
    {
        private static readonly Regex Marker = new(
            @"^(todo|fixme)\b\s*(\([^)]*\))?\s*:?\s*",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex Date = new(
            @"\b\d{4}[-./]\d{1,2}[-./]\d{1,2}\b|\b\d{1,2}[./]\d{1,2}[./]\d{2,4}\b",
            RegexOptions.CultureInvariant);

        private static readonly Regex Ticket = new(@"\b[A-Z][A-Z0-9]+-\d+\b|#\d{2,}", RegexOptions.CultureInvariant);

        private static readonly Regex Author = new(@"@\w+", RegexOptions.CultureInvariant);

        private static readonly Regex Cyrillic = new(@"\p{IsCyrillic}", RegexOptions.CultureInvariant);

        private static readonly Regex Latin = new(@"[A-Za-z]", RegexOptions.CultureInvariant);

        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Bool("forbidMetadata", true, "автор, дата, номер задачи в TODO - находка"),
            CheckParameter.Bool("requireCyrillic", false, "текст TODO - на кириллице"),
        ];

        public override string Name
            => "todo-format";

        public override string Description
            => "формат TODO";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            bool forbidMetadata = context.Settings.GetBool("forbidMetadata");
            bool requireCyrillic = context.Settings.GetBool("requireCyrillic");
            foreach (var comment in CommentTools.GetLineComments(context.Root, context.Text))
            {
                var match = Marker.Match(comment.Content);
                if (!match.Success)
                    continue;

                var task = comment.Content.Substring(match.Length);
                var problems = new List<string>();
                bool isPrefixWrong = !comment.Content.StartsWith("TODO: ", StringComparison.Ordinal);
                if (isPrefixWrong)
                    problems.Add("ожидается 'TODO: '");

                if (forbidMetadata && (match.Groups[2].Success || Author.IsMatch(task)))
                    problems.Add("автор в TODO");

                if (forbidMetadata && Date.IsMatch(task))
                    problems.Add("дата в TODO");

                if (forbidMetadata && Ticket.IsMatch(task))
                    problems.Add("номер задачи в TODO");

                if (requireCyrillic && Latin.IsMatch(task) && !Cyrillic.IsMatch(task))
                    problems.Add("текст TODO не на кириллице");

                if (problems.Count == 0)
                    continue;

                // Замена - только когда неверен лишь префикс
                LineReplacement? replacement = null;
                if (isPrefixWrong && problems.Count == 1 && task.Length > 0)
                    replacement = context.ReplaceInLines(comment.Span, "// TODO: " + task);

                context.Report(comment.Span, string.Join("; ", problems), replacement);
            }
        }
    }
}
