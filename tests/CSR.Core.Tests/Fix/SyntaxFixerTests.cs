using CSR.Core.Config;
using CSR.Core.Fix;
using CSR.Core.Checks;
using CSR.Core.Style;

namespace CSR.Core.Tests.Fix
{
    public class SyntaxFixerTests
    {
        private static readonly ProjectOptions Project = new();

        private const string Source = """
            namespace Demo
            {
                public class Sample
                {
                    /// <summary>
                    /// Документация.
                    /// </summary>
                    private object m_Value;

                    public bool Check(object a, object b)
                    {
                        if (a is null || b is null)
                            return false;

                        return a == b;
                    }
                }
            }

            """;

#region SourceLines

        [Test]
        public void Lines_KeepOwnEndings()
        {
            var lines = SourceLines.Parse("a\r\nb\nc");

            lines.Replace(2, 2, ["x", "y"]);

            Assert.That(lines.Count, Is.EqualTo(4));
            Assert.That(lines.ToString(), Is.EqualTo("a\r\nx\ny\nc"));
        }

        [Test]
        public void Lines_DeleteAndReplaceLast()
        {
            var lines = SourceLines.Parse("a\nb\nc\n");

            lines.Replace(2, 2, []);
            lines.Replace(2, 2, ["z"]);

            Assert.That(lines.ToString(), Is.EqualTo("a\nz\n"));
            Assert.That(lines[2], Is.EqualTo("z"));
        }

#endregion


#region SyntaxFixer

        [Test]
        public void Fix_SeveralReplacementsOnOneLine()
        {
            var result = Run(CreateOptions("null-check"), new FixOptions(), null);

            Assert.That(result.Text, Does.Contain("if (a == null || b == null)"));
            Assert.That(result.Applied, Has.Count.EqualTo(2));
            Assert.That(result.Passes, Is.EqualTo(2));
            Assert.That(result.Remaining, Is.Empty);
        }

        // Замена на несколько строк: находка покрывает их все, строки не размножаются
        [Test]
        public void Fix_MultilineReplacement_LinesNotDuplicated()
        {
            const string source = """
                using System.Collections.Generic;

                public class Sample
                {
                    /// <summary>
                    /// Первая строка
                    /// Скорость — м/с, «вне пути»
                    /// Третья — строка
                    /// </summary>
                    public void Cancel()
                    {
                        Dictionary<int, string> names =
                            new();
                    }
                }
                """;
            var options = CreateOptions("comment-typography", "local-type");

            var result = SyntaxFixer.Fix(source, "Sample.cs", options, Project, new FixOptions(), null);

            Assert.That(result.Text, Is.EqualTo(source
                .Replace(" — ", " - ")
                .Replace("«вне пути»", "\"вне пути\"")
                .Replace("Dictionary<int, string> names", "var names")
                .Replace("new();", "new Dictionary<int, string>();")));
            Assert.That(result.Applied, Has.Count.EqualTo(2));
        }

        [Test]
        public void Fix_DeletesLinesAndShiftsLater()
        {
            var options = CreateOptions("xml-doc", "null-check");
            options.Checks[0].Settings.Set("explanation", "delete");

            var result = SyntaxFixer.Fix(Source, "Sample.cs", options, Project, new FixOptions(), null);

            Assert.That(result.Text, Does.Not.Contain("///"));
            Assert.That(result.Text, Does.Contain("if (a == null || b == null)"));
            Assert.That(result.Text.Split('\n'), Has.Length.EqualTo(Source.Split('\n').Length - 3));
        }

        [Test]
        public void Fix_SeverityAndCheckFilter()
        {
            var options = CreateOptions("xml-doc", "null-check");
            options.Checks[1].Severity = Severity.Hint;

            var errorsOnly = SyntaxFixer.Fix(Source, "Sample.cs", options, Project, new FixOptions(), null);
            var withHints = SyntaxFixer.Fix(
                Source,
                "Sample.cs",
                options,
                Project,
                new FixOptions { MinSeverity = Severity.Hint },
                null);
            var onlyNull = SyntaxFixer.Fix(
                Source,
                "Sample.cs",
                options,
                Project,
                new FixOptions { MinSeverity = Severity.Hint, Checks = new HashSet<string> { "null-check" } },
                null);

            Assert.That(errorsOnly.Text, Does.Contain("a is null"));
            var remaining = errorsOnly.Remaining.Select(finding => finding.Check);
            Assert.That(remaining, Is.EquivalentTo(new[] { "null-check", "null-check" }));
            Assert.That(withHints.Text, Does.Not.Contain("a is null"));
            Assert.That(onlyNull.Text, Does.Contain("///"));
            Assert.That(onlyNull.Remaining.Single().Check, Is.EqualTo("xml-doc"));
        }

        [Test]
        public void Fix_ChangedLinesOnly()
        {
            var options = CreateOptions("xml-doc", "null-check");
            var changed = new ChangedLines();
            changed.Add(12, 1);

            var result = SyntaxFixer.Fix(Source, "Sample.cs", options, Project, new FixOptions(), changed);

            Assert.That(result.Text, Does.Contain("///"), "документация не на измененных строках");
            Assert.That(result.Text, Does.Contain("if (a == null || b == null)"));
            Assert.That(result.Remaining, Is.Empty);
        }

        [Test]
        public void Fix_ChangedLinesFollowDeletedLines()
        {
            var options = CreateOptions("xml-doc", "null-check");
            var changed = new ChangedLines();
            changed.Add(5, 3);
            changed.Add(12, 1);

            var result = SyntaxFixer.Fix(Source, "Sample.cs", options, Project, new FixOptions(), changed);

            Assert.That(result.Text, Does.Not.Contain("///"));
            // строка 12 после удаления документации - строка 9
            Assert.That(result.Text, Does.Contain("if (a == null || b == null)"));
        }

        [Test]
        public void Fix_NothingToFix()
        {
            var result = Run(CreateOptions("line-length"), new FixOptions(), null);

            Assert.That(result.Text, Is.EqualTo(Source));
            Assert.That(result.Passes, Is.Zero);
        }

        [Test]
        public void Fix_RevertsWhenReplacementBreaksSyntax()
        {
            var options = new CheckOptions();
            options.Checks.Add(CheckConfiguration.Create(new BrokenCheck(), Severity.Error));

            var result = Run(options, new FixOptions(), null);

            Assert.That(result.Reverted, Is.True);
            Assert.That(result.Text, Is.EqualTo(Source));
            Assert.That(result.Applied, Is.Empty);
            Assert.That(result.Remaining.Single().Check, Is.EqualTo("broken"));
        }

#endregion

        // Замена на строки 10-16 (метод) - одной строкой сигнатуры: тело метода пропадает
        private class BrokenCheck : SyntaxCheck
        {
            public override string Name
                => "broken";

            public override string Description
                => "замена, которая ломает синтаксис";

            public override void Analyze(CheckContext context)
            {
                if (!context.Text.ToString().Contains("return a == b;"))
                    return;

                var method = context.Text.Lines[9];
                var end = context.Text.Lines[15];
                var span = Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(method.Start, end.End);
                context.Report(span, "сломать", ["        public bool Check(object a, object b)"]);
            }
        }

        private static FixResult Run(CheckOptions options, FixOptions fix, ChangedLines? changed)
            => SyntaxFixer.Fix(Source, "Sample.cs", options, Project, fix, changed);

        private static CheckOptions CreateOptions(params string[] checks)
        {
            var options = new CheckOptions();
            foreach (var check in checks)
                options.Checks.Add(CheckConfiguration.Create(StyleCatalog.Find(check)!, Severity.Error, check));

            return options;
        }
    }
}
