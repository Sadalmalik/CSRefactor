using CSR.Core.Checks;
using CSR.Core.Config;
using CSR.Core.Fix;
using CSR.Core.Style;
using static CSR.Core.Tests.Style.StyleTestCode;

namespace CSR.Core.Tests.Style
{
    // ternary-wrap (F25) и его работа вместе с call-wrap в csr fix
    public class TernaryWrapTests
    {
        [Test]
        public void Ternary_LongAssignment_Column()
        {
            var name = new string('n', 70);
            var body = $"""
                        var speed = c ? Settings.{name}.SprintSpeed : Settings.WalkSpeed;
                """;
            var finding = Run("ternary-wrap", InMethod(body)).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[]
            {
                "        var speed = c",
                $"            ? Settings.{name}.SprintSpeed",
                "            : Settings.WalkSpeed;",
            }));
        }

        [Test]
        public void Ternary_NestedOneLine_EachLevelPlusFour()
        {
            const string body = """
                        side = c ? Ladder() : a > 0 ? Side.End : Side.Start;
                """;
            var finding = Run("ternary-wrap", InMethod(body)).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[]
            {
                "        side = c",
                "            ? Ladder()",
                "            : a > 0",
                "                ? Side.End",
                "                : Side.Start;",
            }));
            Assert.That(Run("ternary-wrap", InMethod(body), settings => settings.Set("nested", false)), Is.Empty);
        }

        [Test]
        public void Ternary_InsideExpression_NoReplacement()
        {
            var name = new string('n', 80);
            var body = $"""
                        var total = a + (c ? Settings.{name} : Settings.WalkSpeed) * 2;
                """;
            var finding = Run("ternary-wrap", InMethod(body)).Single();
            Assert.That(finding.Replacement, Is.Null);
        }

        [Test]
        public void Ternary_TrailingMarks_Found()
        {
            const string body = """
                        var speed = c ?
                            a : 0;
                        var small = c ? a : 0;
                """;
            var findings = Run("ternary-wrap", InMethod(body));
            Assert.That(findings.Single().Message, Does.StartWith("'?' в конце строки"));
        }

        [Test]
        public void Fix_TernaryThenCallInBranch()
        {
            var name = new string('n', 60);
            var source = InMethod($"""
                        var mode = c ? Mode.Walk : Helper.Calculate(o, a, "{name}", 1, 2, 3, 4, 5, 6, 7, 8, 9);
                """);
            var options = new CheckOptions();
            options.Checks.Add(CheckConfiguration.Create(StyleCatalog.Find("ternary-wrap")!, Severity.Error));
            options.Checks.Add(CheckConfiguration.Create(StyleCatalog.Find("call-wrap")!, Severity.Error));

            var result = SyntaxFixer.Fix(source, "Sample.cs", options, new ProjectOptions(), new FixOptions(), null);

            Assert.That(result.Text, Does.Contain("""
                        var mode = c
                            ? Mode.Walk
                            : Helper.Calculate(
                                o,
                """));
            Assert.That(result.Remaining, Is.Empty);
        }
    }
}
