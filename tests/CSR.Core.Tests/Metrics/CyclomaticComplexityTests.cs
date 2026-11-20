using CSR.Core.Metrics;

namespace CSR.Core.Tests.Metrics
{
    public class CyclomaticComplexityTests
    {
        private static int Measure(string body)
            => TestCode.Method(body, MetricKeys.Cyclomatic);

        [Test]
        public void EmptyMethod_IsOne()
            => Assert.That(Measure(""), Is.EqualTo(1));

        [Test]
        public void IfElse_CountsIfOnly()
            => Assert.That(Measure("if (c) Call(); else Call();"), Is.EqualTo(2));

        [Test]
        public void ElseIf_CountsEachIf()
            => Assert.That(Measure("if (c) Call(); else if (d) Call(); else Call();"), Is.EqualTo(3));

        [Test]
        public void Loops_CountOneEach()
        {
            const string body = """
                for (int i = 0; i < a; i++) Call();
                foreach (int item in items) Call();
                foreach (var (x, y) in pairs) Call();
                while (c) Call();
                do Call(); while (d);
                """;
            Assert.That(Measure(body), Is.EqualTo(6));
        }

        [Test]
        public void Catch_CountsEachCatch_FinallyNot()
        {
            const string body = """
                try { Call(); }
                catch (System.InvalidOperationException) { }
                catch (System.Exception) { }
                finally { Call(); }
                """;
            Assert.That(Measure(body), Is.EqualTo(3));
        }

        [Test]
        public void Conditional_CountsOne()
            => Assert.That(Measure("Call(c ? a : b);"), Is.EqualTo(2));

        [Test]
        public void SwitchStatement_CountsCaseLabels_DefaultNot()
        {
            const string body = """
                switch (a)
                {
                    case 1:
                    case 2:
                        break;
                    case > 10:
                        break;
                    default:
                        break;
                }
                """;
            Assert.That(Measure(body), Is.EqualTo(4));
        }

        [Test]
        public void SwitchExpression_CountsEachArm()
            => Assert.That(Measure("Call(a switch { 1 => 1, 2 => 2, _ => 0 });"), Is.EqualTo(4));

        [Test]
        public void LogicalOperators_CountEach()
            => Assert.That(Measure("if (c && d || c && d) Call();"), Is.EqualTo(5));

        [Test]
        public void NullOperators_CountCoalesceAndConditionalAccess()
            => Assert.That(Measure("Call(o ?? o?.ToString()?.Length);"), Is.EqualTo(4));

        [Test]
        public void CoalesceAssignment_NotCounted()
            => Assert.That(Measure("o ??= 1;"), Is.EqualTo(1));

        [Test]
        public void Lambda_CountsInEnclosingFunction()
            => Assert.That(Measure("System.Func<int, int> f = x => x > 0 ? x : -x;"), Is.EqualTo(2));

        [Test]
        public void LocalFunction_CountsSeparately()
        {
            const string source = """
                class Sample
                {
                    void Method(bool c)
                    {
                        if (c) Local(c);

                        void Local(bool flag)
                        {
                            if (flag && c) return;
                        }
                    }
                }
                """;
            Assert.That(TestCode.Function(source, "Sample.Method", MetricKeys.Cyclomatic), Is.EqualTo(2));
            Assert.That(TestCode.Function(source, "Sample.Method.Local", MetricKeys.Cyclomatic), Is.EqualTo(3));
        }

        [Test]
        public void ExpressionBodiedAccessor_IsFunction()
        {
            const string source = """
                class Sample
                {
                    object m_Value;

                    object Value
                    {
                        get => m_Value ?? new object();
                        set { if (value != null) m_Value = value; }
                    }
                }
                """;
            Assert.That(TestCode.Function(source, "Sample.Value.get", MetricKeys.Cyclomatic), Is.EqualTo(2));
            Assert.That(TestCode.Function(source, "Sample.Value.set", MetricKeys.Cyclomatic), Is.EqualTo(2));
        }
    }
}
