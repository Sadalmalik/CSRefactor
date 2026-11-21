using static CSR.Core.Tests.Style.StyleTestCode;

namespace CSR.Core.Tests.Style
{
    public class IdiomChecksTests
    {
#region null-check

        [Test]
        public void NullCheck_IsNull_ToEquality()
        {
            var findings = Run("null-check", InMethod("if (o is null || o is not null) return;"));
            Assert.That(findings, Has.Count.EqualTo(2));
            Assert.That(findings[0].Replacement, Is.EqualTo(new[] { "if (o == null || o is not null) return;" }));
            Assert.That(findings[1].Replacement, Is.EqualTo(new[] { "if (o is null || o != null) return;" }));
        }

        [Test]
        public void NullCheck_CapturePatternAndEquality_NoFinding()
            => Assert.That(Run("null-check", InMethod("if (o is not { } x || o == null) return;")), Is.Empty);

        [Test]
        public void NullCheck_PreferPattern_FlagsEquality()
        {
            var findings = Run("null-check", InMethod("if (o != null) return;"),
                settings => settings.Set("prefer", "pattern"));
            Assert.That(findings.Single().Replacement, Is.EqualTo(new[] { "if (o is not null) return;" }));
        }

#endregion

#region coalesce-assignment

        [Test]
        public void Coalesce_IfAssign_Replaced()
        {
            const string body = """
                if (o == null)
                    o = new object();
                """;
            var finding = Run("coalesce-assignment", InMethod(body)).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "o ??= new object();" }));
        }

        [TestCase("if (o == null) o = 1; else a = 2;")]
        [TestCase("if (o == null) a = 1;")]
        [TestCase("if (o == null) { o = 1; a = 2; }")]
        public void Coalesce_Other_NoFinding(string body)
            => Assert.That(Run("coalesce-assignment", InMethod(body)), Is.Empty);

#endregion

#region out-var

        [Test]
        public void OutVar_Var_Found()
        {
            const string body = """
                if (int.TryParse("1", out var value))
                    a = value;
                """;
            var finding = Run("out-var", InMethod(body)).Single();
            Assert.That(finding.Message, Does.Contain("out var value"));
            Assert.That(finding.Replacement, Is.Null);
        }

        [Test]
        public void OutVar_UnusedVariable_ToDiscard()
        {
            var finding = Run("out-var", InMethod("int.TryParse(\"1\", out int value);")).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "int.TryParse(\"1\", out _);" }));
        }

        [Test]
        public void OutVar_ExplicitReadAndExistingField_NoFinding()
        {
            const string body = """
                if (int.TryParse("1", out int value))
                    a = value;
                int.TryParse("2", out a);
                int.TryParse("3", out _);
                """;
            Assert.That(Run("out-var", InMethod(body)), Is.Empty);
        }

#endregion

#region bool-local-type

        [TestCase("var isAtExit = a >= 10;")]
        [TestCase("var isReady = !c;")]
        [TestCase("var hasValue = o is string;")]
        [TestCase("var both = c && a > 0;")]
        [TestCase("var flag = true;")]
        public void BoolLocal_Var_ToBool(string body)
        {
            var finding = Run("bool-local-type", InMethod(body)).Single();
            Assert.That(finding.Replacement![0], Does.StartWith("bool "));
        }

        [TestCase("bool isAtExit = a >= 10;")]
        [TestCase("var sum = a + 1;")]
        [TestCase("var text = o.ToString();")]
        public void BoolLocal_NoFinding(string body)
            => Assert.That(Run("bool-local-type", InMethod(body)), Is.Empty);

#endregion

#region tuple-type

        [Test]
        public void Tuple_InSignatureAndField_Found()
        {
            const string source = """
                using System.Collections.Generic;

                public class Sample
                {
                    private (int, float) m_Pair;
                    private List<(int, int)> m_Pairs;

                    private static (int, float) Project(int a, (int x, int y) point)
                    {
                        (int, int) local = (1, 2);
                        foreach (var (key, value) in new Dictionary<int, int>())
                            local = (key, value);
                        System.Func<(int, int), int> f = pair => pair.Item1;
                        return (local.Item1, f(point));

                        (int, int) Swap((int a, int b) pair) => (pair.b, pair.a);
                    }
                }
                """;
            var lines = Run("tuple-type", source).Select(finding => finding.Line);
            Assert.That(lines, Is.EqualTo(new[] { 5, 6, 8, 8, 16, 16 }));
        }

#endregion
    }
}
