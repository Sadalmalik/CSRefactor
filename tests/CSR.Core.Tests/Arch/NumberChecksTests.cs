using static CSR.Core.Tests.Arch.ArchTestCode;

namespace CSR.Core.Tests.Arch
{
    public class NumberChecksTests
    {
#region numeric-constant

        [Test]
        public void NumericConstant_HintInName_Found()
        {
            const string source = """
                public class Process
                {
                    private const float EnterRadius = 0.5f;
                    private static readonly float InputDeadZone = 0.15f;
                    private const int MaxRetries = 3;

                    private void Tick()
                    {
                        const float RepathTimeout = 3f;
                    }
                }
                """;
            var findings = Run("numeric-constant", source);
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 3, 4, 9 }));
            Assert.That(findings[0].Message, Is.EqualTo("константа float EnterRadius = 0.5f (подсказка 'Radius')"));
            Assert.That(findings[1].Message, Does.Contain("(подсказка 'DeadZone')"));
        }

        [Test]
        public void NumericConstant_NotNumberOrNoHint_NoFinding()
        {
            const string source = """
                public class Process
                {
                    private const float Epsilon = 0.001f;
                    private const string DefaultSpeed = "fast";
                    private readonly float m_Speed = 1f;
                    private static float s_Radius = 1f;
                }
                """;
            Assert.That(Run("numeric-constant", source), Is.Empty);
        }

        [Test]
        public void NumericConstant_Options_HintsAndReadonly()
        {
            const string source = """
                public class Process
                {
                    private static readonly float InputDeadZone = 0.15f;
                    private const float JumpImpulse = 2f;
                }
                """;
            var findings = Run("numeric-constant", source, settings =>
            {
                settings.Set("staticReadonly", false);
                settings.Set("hints", new[] { "Impulse" });
            });
            Assert.That(findings.Single().Line, Is.EqualTo(4));
        }

#endregion


#region numeric-literal

        [Test]
        public void NumericLiteral_InFormula_Found()
        {
            const string body = """
                if (speed > 0.1f) return;
                var turn = angle - 100f;
                speed += 0.25f;
                var back = angle * -0.3f;
                """;
            var findings = Run("numeric-literal", InMethod(body));
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 5, 6, 7, 8 }));
            Assert.That(findings[0].Message, Is.EqualTo("число 0.1f в 'speed > 0.1f'"));
            Assert.That(findings[3].Message, Does.StartWith("число -0.3f"));
        }

        [TestCase("if (speed > 0 && angle < 1f && speed != -1) return;")]
        [TestCase("var half = speed * 0.5f + speed / 2;")]
        [TestCase("var items = new int[4]; items[2 + 1] = 0;")]
        [TestCase("Call(3.5f);")]
        public void NumericLiteral_NeutralOrNotFormula_NoFinding(string body)
            => Assert.That(Run("numeric-literal", InMethod(body)), Is.Empty);

        [Test]
        public void NumericLiteral_FieldsAndDrawMethods_NoFinding()
        {
            const string source = """
                public class Process
                {
                    private float m_Speed = 3f * 2f;

                    private void OnDrawGizmos()
                    {
                        var size = m_Speed * 0.2f;
                    }

                    private void Run(int value = 4 + 4)
                    {
                    }
                }
                """;
            Assert.That(Run("numeric-literal", source), Is.Empty);
        }

        [Test]
        public void NumericLiteral_Options_NeutralAndIntegers()
        {
            var source = InMethod("if (speed > 0.0001f || angle > 90 || speed < 1e-6) return;");
            Assert.That(Run("numeric-literal", source), Has.Count.EqualTo(3));
            var findings = Run("numeric-literal", source, settings =>
            {
                settings.Set("neutral", new[] { "0.000*", "*e-*" });
                settings.Set("integers", false);
            });
            Assert.That(findings, Is.Empty);
        }

#endregion
    }
}
