using System.Text.RegularExpressions;

namespace CSR.Core.Style.Checks
{
    // Комментарий-разделитель: серия знаков в начале комментария (// --- Section ---, // =====)
    public class CommentSeparatorCheck : StyleCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("minRun", 3, "наименьшая длина серии знаков - = * _ # ~ + / в начале комментария"),
        ];

        public override string Name
            => "comment-separator";

        public override string Description
            => "комментарий-разделитель";

        public override IReadOnlyList<CheckParameter> Parameters
            => AllParameters;

        public override void Analyze(CheckContext context)
        {
            var run = context.Settings.GetInt("minRun");
            var pattern = new Regex($"^[-=*_#~+/]{{{run},}}", RegexOptions.CultureInvariant);
            foreach (var comment in Comments.GetLineComments(context.Root, context.Text))
            {
                if (comment.IsFullLine && pattern.IsMatch(comment.Content))
                    context.Report(comment.Span, "комментарий-разделитель");
            }
        }
    }
}
