using static CSR.Core.Tests.Style.StyleTestCode;

namespace CSR.Core.Tests.Style
{
    // Проверки переносов и пустых строк с заменами: expression-arrow, signature-wrap, call-wrap, region-layout
    public class WrapChecksTests
    {
#region expression-arrow

        [Test]
        public void Arrow_PropertyOnOneLine_Split()
        {
            const string source = """
                public class Sample
                {
                    public int Value => m_Value;
                    public int Next
                        => m_Value + 1;
                }
                """;
            var finding = Run("expression-arrow", source).Single();
            Assert.That(finding.Line, Is.EqualTo(3));
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "    public int Value", "        => m_Value;" }));
        }

        [Test]
        public void Arrow_TrailingArrow_MovedToNextLine()
        {
            const string source = """
                public class Sample
                {
                    public int Sum(int a, int b) =>
                        a + b;
                }
                """;
            var finding = Run("expression-arrow", source).Single();
            Assert.That((finding.Line, finding.EndLine), Is.EqualTo((3, 4)));
            Assert.That(
                finding.Replacement,
                Is.EqualTo(new[] { "    public int Sum(int a, int b)", "        => a + b;" }));
        }

        [Test]
        public void Arrow_MethodFitsOrTooLong()
        {
            var tail = new string('x', 100);
            var source = $$"""
                public class Sample
                {
                    public int Short() => 1;
                    public string Long() => "{{tail}}";
                }
                """;
            var finding = Run("expression-arrow", source).Single();
            Assert.That(finding.Line, Is.EqualTo(4));
            Assert.That(finding.Replacement![1], Is.EqualTo($"        => \"{tail}\";"));
            Assert.That(
                Run("expression-arrow", source, settings => settings.Set("methods", "next-line")),
                Has.Count.EqualTo(2));
        }

        [Test]
        public void Arrow_AnyAndAccessors_NoFinding()
        {
            const string source = """
                public class Sample
                {
                    public int Value => m_Value;
                    public int Other { get => 1; }
                }
                """;
            Assert.That(Run("expression-arrow", source, settings => settings.Set("properties", "any")), Is.Empty);
        }

#endregion


#region signature-wrap

        [Test]
        public void Signature_FiveParameters_ColumnWithBase()
        {
            const string source = """
                public class Sample : Base
                {
                    public Sample(int a, int b, int c, int d, int e) : base(a, b)
                    {
                    }
                }
                """;
            var finding = Run("signature-wrap", source).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[]
            {
                "    public Sample(",
                "        int a,",
                "        int b,",
                "        int c,",
                "        int d,",
                "        int e)",
                "        : base(a, b)",
            }));
        }

        [Test]
        public void Signature_MidListWrapAndCloseParen()
        {
            const string source = """
                public class Sample
                {
                    [Obsolete("old")]
                    public void Mid(int a,
                        int b)
                    {
                    }

                    public void Close(
                        int a,
                        int b
                    )
                    {
                    }
                }
                """;
            var findings = Run("signature-wrap", source);
            Assert.That(
                findings.Select(finding => (finding.Line, finding.EndLine)),
                Is.EqualTo(new[] { (4, 5), (9, 12) }));
            Assert.That(
                findings[0].Replacement,
                Is.EqualTo(new[] { "    public void Mid(", "        int a,", "        int b)" }));
            Assert.That(findings[1].Replacement![2], Is.EqualTo("        int b)"));
        }

        [Test]
        public void Signature_LongLineAndFourParameters()
        {
            var name = new string('N', 90);
            var source = $$"""
                public class Sample
                {
                    public void {{name}}(int first, int second) { }
                    public void Four(int a, int b, int c, int d) { }
                }
                """;
            var finding = Run("signature-wrap", source).Single();
            Assert.That(finding.Line, Is.EqualTo(3));
            Assert.That(finding.Replacement![2], Is.EqualTo("        int second) { }"));
        }

#endregion


#region call-wrap

        [Test]
        public void Call_LongLine_OuterCallInColumn()
        {
            var text = new string('t', 60);
            var body = $"""
                        var result = Helper.Calculate(o, Helper.Transform(a, 10), "{text}", 42);
                """;
            var finding = Run("call-wrap", InMethod(body)).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[]
            {
                "        var result = Helper.Calculate(",
                "            o,",
                "            Helper.Transform(a, 10),",
                $"            \"{text}\",",
                "            42);",
            }));
        }

        [Test]
        public void Call_MidListWrap()
        {
            const string body = """
                        Helper.Calculate(o,
                            a);
                """;
            var finding = Run("call-wrap", InMethod(body)).Single();
            Assert.That(
                finding.Replacement,
                Is.EqualTo(new[] { "        Helper.Calculate(", "            o,", "            a);" }));
        }

        [Test]
        public void Call_TabIndentedFile_TabLevel()
        {
            var source = "public class Sample\n{\n\tvoid M()\n\t{\n\t\tHelper.Calculate(o,\n\t\t\ta);\n\t}\n}\n";
            var finding = Run("call-wrap", source).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "\t\tHelper.Calculate(", "\t\t\to,", "\t\t\ta);" }));
        }

        [Test]
        public void Call_InsideTernaryOrBinary_NoReplacement()
        {
            var name = new string('n', 60);
            var body = $"""
                        var mode = c && Helper.IsSprinting(o, a, "{name}") ? Mode.Sprint : Mode.Walk;
                        var size = a + Helper.Measure(o, a, "{name}", 1, 2, 3, 4, 5, 6, 7) * 2;
                        var next = c ? Mode.None : Helper.GetSection(o, a, "{name}", 1, 2, 3, 4, 5);
                """;
            var findings = Run("call-wrap", InMethod(body));
            Assert.That(findings, Has.Count.EqualTo(3));
            Assert.That(findings.All(finding => finding.Replacement == null));
            Assert.That(findings[0].Message, Does.Contain("вызов внутри выражения"));
        }

        [Test]
        public void Call_ArgumentOfHeadCall_Replaced()
        {
            var name = new string('n', 70);
            var body = $"""
                        result = Helper.Calculate(Helper.Transform(o, a), "{name}");
                """;
            var finding = Run("call-wrap", InMethod(body)).Single();
            Assert.That(finding.Replacement![0], Is.EqualTo("        result = Helper.Calculate("));
        }

        [Test]
        public void Call_ConditionLoggerAndLambda_NoFinding()
        {
            var text = new string('t', 100);
            var body = $$"""
                        if (Helper.Check(o, a, "{{text}}"))
                            return;

                        Debug.Log("{{text}}", o);
                        Helper.Run(o, () =>
                        {
                            a++;
                        });
                """;
            Assert.That(Run("call-wrap", InMethod(body)), Is.Empty);
        }

#endregion


#region region-layout

        [Test]
        public void Region_LayoutFindings()
        {
            const string source = """
                public class Sample
                {

                #region State
                    private int m_Value;
                #endregion

                #region Logic

                    private void Run()
                    {
                    }

                #endregion

                }
                """;
            var findings = Run("region-layout", source);
            Assert.That(findings.Select(finding => finding.Message), Is.EqualTo(new[]
            {
                "перед #region: пустых строк 1, нужно 0",
                "после #region: пустых строк 0, нужно 1",
                "перед #endregion: пустых строк 0, нужно 1",
                "после #endregion: пустых строк 1, нужно 2",
                "после #endregion: пустых строк 1, нужно 0",
            }));
            Assert.That(findings[3].Replacement, Is.EqualTo(new[] { "#endregion", "", "", "#region Logic" }));
        }

        [Test]
        public void Region_Correct_NoFinding()
        {
            const string source = """
                public class Sample
                {
                #region State

                    private int m_Value;

                #endregion


                #region Logic

                    private void Run()
                    {
                    }

                #endregion

                    private int m_Outside;
                }
                """;
            Assert.That(Run("region-layout", source), Is.Empty);
        }

#endregion
    }
}
