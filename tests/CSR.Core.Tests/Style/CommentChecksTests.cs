using static CSR.Core.Tests.Style.StyleTestCode;

namespace CSR.Core.Tests.Style
{
    public class CommentChecksTests
    {
#region xml-doc

        [Test]
        public void XmlDoc_SummaryBlock_DeletesLines()
        {
            const string source = """
                public class Sample
                {
                    /// <summary>
                    /// Отменяет движение
                    /// </summary>
                    public void Cancel() { }
                }
                """;
            var finding = Run("xml-doc", source).Single();
            Assert.That(finding.Line, Is.EqualTo(3));
            Assert.That(finding.EndLine, Is.EqualTo(5));
            Assert.That(finding.Rule, Is.EqualTo("R1"));
            Assert.That(finding.Replacement, Is.Empty);
        }

        [Test]
        public void XmlDoc_PlainComments_NoFinding()
        {
            const string source = """
                public class Sample
                {
                    // Список переписывается следующим вызовом
                    //// четыре черты - не документация
                    public void Cancel() { }
                }
                """;
            Assert.That(Run("xml-doc", source), Is.Empty);
        }

#endregion

#region comment-separator

        [TestCase("// ===== Stuck detection =====")]
        [TestCase("// --- Section ---")]
        [TestCase("// ******")]
        [TestCase("//-------------------------------")]
        public void Separator_Found(string comment)
            => Assert.That(Run("comment-separator", InMethod(comment)), Has.Count.EqualTo(1));

        [TestCase("// сколько секунд разрешено не приближаться - к следующей точке")]
        [TestCase("// a -> b")]
        [TestCase("// Разделитель: // --- Section ---, // ****")]
        public void Separator_PlainComment_NoFinding(string comment)
            => Assert.That(Run("comment-separator", InMethod(comment)), Is.Empty);

#endregion

#region comment-typography

        [Test]
        public void Typography_InComment_ReplacesAll()
        {
            var finding = Run("comment-typography", InMethod("// 0 — начало, 1 → конец, «вне пути»")).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "// 0 - начало, 1 -> конец, \"вне пути\"" }));
        }

        [Test]
        public void Typography_InLogAndToString_Found()
        {
            const string source = """
                public class Sample
                {
                    private void Method()
                    {
                        Debug.Log($"шаг {1} → {2}");
                        var text = "a → b";
                    }

                    public override string ToString()
                        => $"{nameof(Sample)} 1 → 2";
                }
                """;
            var lines = Run("comment-typography", source).Select(finding => finding.Line);
            Assert.That(lines, Is.EqualTo(new[] { 5, 10 }));
        }

        [Test]
        public void Typography_TooltipAttribute_Found()
        {
            const string source = """
                public class Sample
                {
                    [Tooltip("Скорость — м/с")]
                    [Other("Скорость — м/с")]
                    private float m_Speed;
                }
                """;
            Assert.That(Run("comment-typography", source).Single().Line, Is.EqualTo(3));
        }

#endregion

#region comment-ending

        [Test]
        public void Ending_TrailingPeriod_Removed()
        {
            var finding = Run("comment-ending", InMethod("// Очередь чистится раньше.")).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "// Очередь чистится раньше" }));
        }

        [Test]
        public void Ending_LetterYo_Replaced()
        {
            var finding = Run("comment-ending", InMethod("// Чистится раньше, чем её читает двигатель")).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "// Чистится раньше, чем ее читает двигатель" }));
        }

        [TestCase("// Точка между предложениями. Остается")]
        [TestCase("// Многоточие...")]
        [TestCase("// Без точки")]
        public void Ending_NoFinding(string comment)
            => Assert.That(Run("comment-ending", InMethod(comment)), Is.Empty);

        [Test]
        public void Ending_PeriodInsideBlock_OnlyWithoutStandaloneOnly()
        {
            const string body = """
                // Да, hide не используется.
                // Их нет смысла маскировать.
                """;
            Assert.That(Run("comment-ending", InMethod(body)), Is.Empty);
            var all = Run("comment-ending", InMethod(body), settings => settings.Set("standaloneOnly", false));
            Assert.That(all, Has.Count.EqualTo(2));
        }

        [Test]
        public void Ending_PreferPeriod_AddsPeriod()
        {
            var findings = Run("comment-ending", InMethod("// Без точки"),
                settings => settings.Set("prefer", "period"));
            Assert.That(findings.Single().Replacement, Is.EqualTo(new[] { "// Без точки." }));
        }

#endregion

#region commented-code

        [Test]
        public void CommentedCode_StatementLine_Found()
        {
            const string body = """
                // старый расчет, оставил на всякий случай
                // var speed = a * 2;
                Method(o, a, c);
                """;
            var finding = Run("commented-code", InMethod(body)).Single();
            Assert.That(finding.Line, Is.EqualTo(8));
            Assert.That(finding.Replacement, Is.Empty);
        }

        [Test]
        public void CommentedCode_Block_OneFinding()
        {
            const string body = """
                // if (c)
                // {
                //     Method(o, a, c);
                // }
                """;
            var finding = Run("commented-code", InMethod(body)).Single();
            Assert.That(finding.Line, Is.EqualTo(7));
            Assert.That(finding.EndLine, Is.EqualTo(10));
        }

        [Test]
        public void CommentedCode_TrailingTail_ReplacedWithCode()
        {
            var finding = Run("commented-code", InMethod("a += 1; // * weight;")).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "a += 1;" }));
        }

        [TestCase("// Мы движемся слишком медленно - вероятно мы застряли")]
        [TestCase("// TODO: вернуть var speed = a * 2;")]
        [TestCase("// см. Method(o, a, c)")]
        public void CommentedCode_Prose_NoFinding(string comment)
            => Assert.That(Run("commented-code", InMethod(comment)), Is.Empty);

#endregion

#region todo-format

        [Test]
        public void Todo_WrongPrefix_ReplacedWhenOnlyProblem()
        {
            var finding = Run("todo-format", InMethod("//todo сгладить поворот")).Single();
            Assert.That(finding.Replacement, Is.EqualTo(new[] { "// TODO: сгладить поворот" }));
        }

        [Test]
        public void Todo_DateTicketAuthor_Found()
        {
            var finding = Run("todo-format", InMethod("// TODO(gleb): 2025-03-12 TASK-1234 fix smoothing")).Single();
            Assert.That(finding.Message, Does.Contain("автор"));
            Assert.That(finding.Message, Does.Contain("дата"));
            Assert.That(finding.Message, Does.Contain("номер задачи"));
            Assert.That(finding.Replacement, Is.Null);
        }

        [Test]
        public void Todo_RequireCyrillic()
        {
            var findings = Run("todo-format", InMethod("// TODO: fix smoothing"),
                settings => settings.Set("requireCyrillic", true));
            Assert.That(findings.Single().Message, Does.Contain("кириллице"));
        }

        [Test]
        public void Todo_Correct_NoFinding()
            => Assert.That(Run("todo-format", InMethod("// TODO: дописать смещение вдоль пути")), Is.Empty);

#endregion
    }
}
