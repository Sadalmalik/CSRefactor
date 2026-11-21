using static CSR.Core.Tests.Style.StyleTestCode;

namespace CSR.Core.Tests.Style
{
    public class LocalTypeCheckTests
    {
        [TestCase("Vector tangent = GetTangent(a);", "var tangent = GetTangent(a);")]
        [TestCase("IReadOnlyList<Area> areas = o.Wall.Areas;", "var areas = o.Wall.Areas;")]
        [TestCase("float[] buffer = ArrayPool<float>.Shared.Rent(a);", "var buffer = ArrayPool<float>.Shared.Rent(a);")]
        [TestCase("List<int> cells = new List<int>();", "var cells = new List<int>();")]
        [TestCase("List<int> cells = new();", "var cells = new List<int>();")]
        [TestCase("Path path = (Path)o;", "var path = (Path)o;")]
        [TestCase("Path path = o as Path;", "var path = o as Path;")]
        [TestCase("string text = $\"{a}\";", "var text = $\"{a}\";")]
        [TestCase("using Stream stream = Open();", "using var stream = Open();")]
        [TestCase("Drain drain = o.Wall?.Drain;", "var drain = o.Wall?.Drain;")]
        [TestCase("Path path = c ? new Path() : (Path)o;", "var path = c ? new Path() : (Path)o;")]
        [TestCase("string text = c ? \"a\" : $\"{a}\";", "var text = c ? \"a\" : $\"{a}\";")]
        public void Explicit_ToVar(string body, string expected)
        {
            var finding = Run("local-type", InMethod(body)).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[] { expected }));
        }

        [TestCase("bool isAtEnter = a <= 0;")]
        [TestCase("int count = 0;")]
        [TestCase("float speed = a * 2f;")]
        [TestCase("object boxed = a;")]
        [TestCase("Path best = default;")]
        [TestCase("Path best = null;")]
        [TestCase("Path? maybe = Find();")]
        [TestCase("Path path;")]
        [TestCase("Shape shape = new Circle();")]
        [TestCase("IEnumerable<int> items = new[] { 1, 2 };")]
        [TestCase("List<int> items = [];")]
        [TestCase("Func<int, int> twice = x => x * 2;")]
        [TestCase("Path path = c ? Find() : null;")]
        [TestCase("SyntaxNode node = c ? access : name;")]
        [TestCase("Shape shape = c ? FindCircle() : FindSquare();")]
        [TestCase("Path path = c ? new Path() : Find();")]
        [TestCase("Span<int> span = stackalloc int[4];")]
        [TestCase("const string Name = \"a\";")]
        [TestCase("Path first = Find(), second = Find();")]
        [TestCase("var path = Find();")]
        [TestCase("IReadOnlyList<int> items = Array.Empty<int>();\nitems = new List<int>();")]
        [TestCase("IReadOnlyList<int> items = Array.Empty<int>();\nRead(out items);")]
        [TestCase("IReadOnlyList<int> items = Array.Empty<int>();\nRead(ref items);")]
        public void Exceptions_NoFinding(string body)
            => Assert.That(Run("local-type", InMethod(body)), Is.Empty);

        [Test]
        public void InArgument_NotReassignment_Found()
        {
            var findings = Run("local-type", InMethod("Path path = Find();\nRead(in path);"));
            Assert.That(findings.Single().Message, Does.Contain("var path"));
        }

        [Test]
        public void Foreach_ExplicitElementType_ToVar()
        {
            const string body = """
                foreach (Cell cell in o.Cells)
                    a++;
                foreach (int index in o.Indices)
                    a++;
                foreach (Transform child in o.Root)
                    a++;
                """;
            var findings = Run(
                "local-type",
                InMethod(body),
                settings => settings.Set("foreachExplicitTypes", new[] { "Transform" }));
            Assert.That(findings.Single().Replacement, Is.EqualTo(new[] { "foreach (var cell in o.Cells)" }));
        }

        [Test]
        public void PreferExplicit_FlagsVarWithHiddenType()
        {
            const string body = """
                var path = Find();
                var cells = new List<int>();
                """;
            var findings = Run("local-type", InMethod(body), settings => settings.Set("prefer", "explicit"));
            Assert.That(findings.Single().Line, Is.EqualTo(7));
            Assert.That(findings[0].Replacement, Is.Null);
        }
    }
}
