using static CSR.Core.Tests.Style.StyleTestCode;

namespace CSR.Core.Tests.Style
{
    public class FormattingAndLogChecksTests
    {
#region line-length

        [Test]
        public void LineLength_LongCode_Found()
        {
            var line = "a = " + string.Join(" + ", Enumerable.Repeat("a", 60)) + ";";
            var finding = Run("line-length", InMethod(line)).Single();
            Assert.That(finding.Column, Is.EqualTo(121));
            Assert.That(finding.Message, Does.Contain(line.Length.ToString()));
        }

        [Test]
        public void LineLength_CommentAndStringLines_Ignored()
        {
            var text = new string('x', 130);
            var body = $"// {text}\nMethod(o,\n    \"{text}\", c);";
            Assert.That(Run("line-length", InMethod(body)), Is.Empty);
        }

        [Test]
        public void LineLength_CustomMax()
        {
            var line = "a = " + new string('1', 60) + ";";
            var findings = Run("line-length", InMethod(line), settings => settings.Set("max", 60));
            Assert.That(findings.Single().Line, Is.EqualTo(7));
        }

#endregion

#region directive-column

        [Test]
        public void Directive_Indented_Unindented()
        {
            const string source = """
                public class Sample
                {
                    #region Fields
                    private int m_Value;
                #endregion

                    private void Method()
                    {
                        #if UNITY_EDITOR
                        m_Value = 1;
                #endif
                    }
                }
                """;
            var findings = Run("directive-column", source);
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 3, 9 }));
            Assert.That(findings[0].Replacement, Is.EqualTo(new[] { "#region Fields" }));
        }

#endregion

#region binary-op-wrap

        [Test]
        public void BinaryWrap_TrailingOperator_Found()
        {
            const string body = """
                if (c &&
                    a > 0)
                    return;
                if (c
                    || a > 0)
                    return;
                """;
            var finding = Run("binary-op-wrap", InMethod(body)).Single();
            Assert.That((finding.Line, finding.EndLine), Is.EqualTo((7, 8)));
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "if (c", "    && a > 0)" }));
        }

        [Test]
        public void BinaryWrap_PreferTrailing()
        {
            const string body = """
                if (c
                    || a > 0)
                    return;
                """;
            var finding = Run(
                "binary-op-wrap",
                InMethod(body),
                settings => settings.Set("prefer", "trailing")).Single();
            Assert.That((finding.Line, finding.EndLine), Is.EqualTo((7, 8)));
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "if (c ||", "    a > 0)" }));
        }

#endregion

#region log-call-wrap

        [Test]
        public void LogWrap_MultilineCall_Found()
        {
            const string body = """
                Debug.Log($"шаг {a} " +
                    $"дальше {c}");
                """;
            Assert.That(Run("log-call-wrap", InMethod(body)).Single().Line, Is.EqualTo(7));
        }

        [Test]
        public void LogWrap_ArgumentsOnNextLine_NoFinding()
        {
            const string body = """
                Debug.Log(
                    $"нет цели '{a}' в '{c}'");
                Debug.Log("одна строка");
                Other.Log(
                    "не логгер",
                    "дальше");
                """;
            Assert.That(Run("log-call-wrap", InMethod(body)), Is.Empty);
        }

        [Test]
        public void LogWrap_LongSingleLine_FoundWithMaxLength()
        {
            var body = $"Debug.Log(\"{new string('x', 120)}\");";
            Assert.That(Run("log-call-wrap", InMethod(body)), Is.Empty);
            var findings = Run("log-call-wrap", InMethod(body), settings => settings.Set("maxLength", 120));
            Assert.That(findings.Single().Message, Does.Contain("сократить текст"));
        }

#endregion

#region message-text

        [Test]
        public void Message_ConcatenationAndPeriod_Found()
        {
            const string body = """
                Debug.LogError("Slot " + a + " is out of range.");
                throw new ArgumentOutOfRangeException(nameof(a), $"Slot {a} is out of range.");
                """;
            var findings = Run("message-text", InMethod(body));
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 7, 7, 8 }));
            var expected = "throw new ArgumentOutOfRangeException(nameof(a), $\"Slot {a} is out of range\");";
            Assert.That(findings[2].Replacement, Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void Message_ProjectLoggerMask_Found()
            => Assert.That(Run("message-text", InMethod("Combat.Logger.Error(\"Ошибка.\");")), Has.Count.EqualTo(1));

        [TestCase("Debug.Log($\"{nameof(Sample)} No slot was selected!\");")]
        [TestCase("Debug.Log(\"Ждем...\");")]
        [TestCase("Other.Write(\"a\" + a + \".\");")]
        public void Message_Correct_NoFinding(string body)
            => Assert.That(Run("message-text", InMethod(body)), Is.Empty);

#endregion
    }
}
