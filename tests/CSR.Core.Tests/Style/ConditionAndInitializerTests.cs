using static CSR.Core.Tests.Style.StyleTestCode;

namespace CSR.Core.Tests.Style
{
    // condition-wrap (F24) и initializer-layout (F23)
    public class ConditionAndInitializerTests
    {
#region condition-wrap

        [Test]
        public void Condition_LongReturnChain_OperandPerLine()
        {
            const string body = """
                        return o == null || o.IsDisposed || !o.IsAlive || !o.IsInGame || !o.IsActive || !o.IsVisible || !o.Queue.IsCurrent(command);
                """;
            var finding = Run("condition-wrap", InMethod(body)).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[]
            {
                "        return o == null",
                "            || o.IsDisposed",
                "            || !o.IsAlive",
                "            || !o.IsInGame",
                "            || !o.IsActive",
                "            || !o.IsVisible",
                "            || !o.Queue.IsCurrent(command);",
            }));
        }

        [Test]
        public void Condition_LongIfMixedOperators_TopLevelOnly()
        {
            var name = new string('n', 70);
            var body = $"""
                        if ((a > 0 && c) || {name}.IsReady(a) || o == null)
                            return;
                """;
            var finding = Run("condition-wrap", InMethod(body)).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[]
            {
                "        if ((a > 0 && c)",
                $"            || {name}.IsReady(a)",
                "            || o == null)",
            }));
        }

        [Test]
        public void Condition_ArgumentsAtBodyLevel_MovedToPlusEight()
        {
            const string body = """
                        if (!TryFind(o,
                            out int index,
                            out float distance))
                            return;
                """;
            var findings = Run("condition-wrap", InMethod(body));
            Assert.That(findings.Select(finding => finding.Replacement!.Single()), Is.EqualTo(new[]
            {
                "                out int index,",
                "                out float distance))",
            }));
        }

        [Test]
        public void Condition_ShortOrWrapped_NoFinding()
        {
            const string body = """
                        if (a > 0 && c)
                            return;

                        if (a > 0
                            && Check(o,
                                    a))
                            return;
                """;
            Assert.That(Run("condition-wrap", InMethod(body)), Is.Empty);
        }

#endregion


#region initializer-layout

        [Test]
        public void Initializer_BraceOnNewLineAndTrailingComma()
        {
            const string body = """
                        var step = new StepResult {
                            Move = a,
                            Look = c
                        };
                """;
            var finding = Run("initializer-layout", InMethod(body)).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[]
            {
                "        var step = new StepResult",
                "        {",
                "            Move = a,",
                "            Look = c,",
                "        };",
            }));
        }

        [Test]
        public void Initializer_TwoElementsOnLine()
        {
            const string body = """
                        var step = new StepResult
                        {
                            Move = a, Look = c,
                        };
                """;
            var finding = Run("initializer-layout", InMethod(body)).Single();
            Assert.That(finding.Message, Does.StartWith("два элемента"));
            Assert.That(finding.Replacement![2], Is.EqualTo("            Move = a,"));
        }

        [Test]
        public void Initializer_LongOneLine_ShortAllowed()
        {
            var value = new string('v', 90);
            var body = $$"""
                        var items = new[] { -1, 0, 1 };
                        Use(new Options { Name = "{{value}}", Count = 2 });
                """;
            var finding = Run("initializer-layout", InMethod(body)).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[]
            {
                "        Use(new Options",
                "        {",
                $"            Name = \"{value}\",",
                "            Count = 2,",
                "        });",
            }));
        }

        [Test]
        public void Initializer_NoTrailingCommaRequired()
        {
            const string body = """
                        var step = new StepResult
                        {
                            Move = a,
                            Look = c
                        };
                """;
            var findings = Run("initializer-layout", InMethod(body), settings => settings.Set("trailingComma", false));
            Assert.That(findings, Is.Empty);
        }

#endregion
    }
}
