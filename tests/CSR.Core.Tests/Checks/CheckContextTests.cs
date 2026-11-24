using CSR.Core.Checks;
using CSR.Core.Config;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Tests.Checks
{
    public class CheckContextTests
    {
        private const string Source = "a\nb\nc\nd\n";

        // Находка шире замены: замена дополняется исходными строками находки
        [Test]
        public void Report_SpanWiderThanReplacement_ReplacementWidened()
        {
            var context = CreateContext(out List<Finding> findings);
            var span = TextSpan.FromBounds(context.Text.Lines[0].Start, context.Text.Lines[2].End);
            var changed = new TextSpan(context.Text.Lines[1].Start, 1);

            context.Report(span, "m", context.ReplaceInLines(changed, "x"));

            var finding = findings.Single();
            Assert.That((finding.Line, finding.EndLine), Is.EqualTo((1, 3)));
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "a", "x", "c" }));
        }

        // Находка уже замены: строки находки расширяются до строк замены, столбец - с начала строки
        [Test]
        public void Report_SpanNarrowerThanReplacement_LinesWidened()
        {
            var context = CreateContext(out List<Finding> findings);
            var span = new TextSpan(context.Text.Lines[2].Start, 1);
            var merged = TextSpan.FromBounds(context.Text.Lines[1].End, context.Text.Lines[3].Start);

            context.Report(span, "m", context.ReplaceInLines(merged, " "));

            var finding = findings.Single();
            Assert.That((finding.Line, finding.EndLine, finding.Column), Is.EqualTo((2, 4, 1)));
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "b d" }));
        }

        [Test]
        public void Report_SameLines_Unchanged()
        {
            var context = CreateContext(out List<Finding> findings);
            var span = new TextSpan(context.Text.Lines[1].Start, 1);

            context.Report(span, "m", context.ReplaceInLines(span, "z"));

            var finding = findings.Single();
            Assert.That((finding.Line, finding.EndLine, finding.Column), Is.EqualTo((2, 2, 1)));
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "z" }));
        }

        private static CheckContext CreateContext(out List<Finding> findings)
        {
            findings = new List<Finding>();
            var tree = CSharpSyntaxTree.ParseText(Source, path: "Sample.cs");
            var configuration = CheckConfiguration.Create(new EmptyCheck(), Severity.Warning);
            return new CheckContext(tree, new ProjectOptions(), configuration, findings);
        }

        private class EmptyCheck : SyntaxCheck
        {
            public override string Name
                => "empty";

            public override string Description
                => "";

            public override void Analyze(CheckContext context)
            {
            }
        }
    }
}
