using CSR.Reports.Diff;

namespace CSR.Reports.Tests
{
    public class LineDiffTests
    {
        [Test]
        public void Diff_ChangedLine()
        {
            var diff = LineDiff.Build("f.cs", "a\nb\nc\nd\ne\nf\ng\nh\n", "a\nb\nc\nd\nX\nf\ng\nh\n");

            Assert.That(diff, Is.EqualTo("--- a/f.cs\n+++ b/f.cs\n@@ -2,7 +2,7 @@\n b\n c\n d\n-e\n+X\n f\n g\n h\n"));
        }

        [Test]
        public void Diff_DeletedAndInsertedLines()
        {
            var deleted = LineDiff.Build("f.cs", "a\nb\nc\n", "a\nc\n");
            var inserted = LineDiff.Build("f.cs", "a\nc\n", "a\nb\nc\n");

            Assert.That(deleted, Is.EqualTo("--- a/f.cs\n+++ b/f.cs\n@@ -1,3 +1,2 @@\n a\n-b\n c\n"));
            Assert.That(inserted, Is.EqualTo("--- a/f.cs\n+++ b/f.cs\n@@ -1,2 +1,3 @@\n a\n+b\n c\n"));
        }

        [Test]
        public void Diff_DistantChangesTwoHunks()
        {
            var before = string.Concat(Enumerable.Range(1, 20).Select(index => $"line{index}\n"));
            var after = before.Replace("line2\n", "two\n").Replace("line18\n", "eighteen\n");

            var diff = LineDiff.Build("f.cs", before, after);

            Assert.That(diff, Does.Contain("@@ -1,5 +1,5 @@"));
            Assert.That(diff, Does.Contain("@@ -15,6 +15,6 @@"));
            Assert.That(diff, Does.Contain("-line18\n+eighteen\n"));
        }

        [Test]
        public void Diff_CrLfAndEqual()
        {
            Assert.That(LineDiff.Build("f.cs", "a\r\nb\r\n", "a\r\nb\r\n"), Is.Empty);
            var diff = LineDiff.Build("f.cs", "a\r\nb\r\n", "a\r\nc\r\n");
            Assert.That(diff, Does.EndWith("@@ -1,2 +1,2 @@\n a\n-b\n+c\n"));
        }

        // Тексты отличаются только переводом в конце: дифф не пустой, у строки без перевода - маркер
        [Test]
        public void Diff_OnlyFinalNewLine()
        {
            const string marker = "\\ No newline at end of file\n";

            var added = LineDiff.Build("f.cs", "a\nb", "a\nb\n");
            var removed = LineDiff.Build("f.cs", "a\nb\n", "a\nb");

            Assert.That(added, Is.EqualTo($"--- a/f.cs\n+++ b/f.cs\n@@ -1,2 +1,2 @@\n a\n-b\n{marker}+b\n"));
            Assert.That(removed, Is.EqualTo($"--- a/f.cs\n+++ b/f.cs\n@@ -1,2 +1,2 @@\n a\n-b\n+b\n{marker}"));
        }

        // Последняя строка без перевода в обоих текстах - в контексте с маркером
        [Test]
        public void Diff_NoEndingInContext()
        {
            var diff = LineDiff.Build("f.cs", "a\nb", "x\nb");

            Assert.That(diff, Does.EndWith("@@ -1,2 +1,2 @@\n-a\n+x\n b\n\\ No newline at end of file\n"));
        }

        [Test]
        public void Diff_EmptyToText()
        {
            var diff = LineDiff.Build("f.cs", "", "a\n");

            Assert.That(diff, Does.Contain("@@ -0,0 +1,1 @@\n+a\n"));
        }
    }
}
