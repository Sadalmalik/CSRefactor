using CSR.Core.Metrics;

namespace CSR.Core.Tests.Metrics
{
    public class CognitiveComplexityTests
    {
        private static int Measure(string body)
            => TestCode.Method(body, MetricKeys.Cognitive);

        [Test]
        public void EmptyMethod_IsZero()
            => Assert.That(Measure(""), Is.EqualTo(0));

        // Пример sumOfPrimes из спецификации: continue с меткой заменен на goto
        [Test]
        public void SpecSumOfPrimes_IsSeven()
        {
            const string body = """
                for (int i = 1; i <= a; ++i)
                {
                    for (int j = 2; j < i; ++j)
                    {
                        if (i % j == 0)
                            goto Next;
                    }

                    b += i;
                    Next: ;
                }
                """;
            Assert.That(Measure(body), Is.EqualTo(7));
        }

        // Пример getWords из спецификации: switch - одна надбавка на весь оператор
        [Test]
        public void SpecGetWords_IsOne()
        {
            const string body = """
                switch (a)
                {
                    case 1:
                        Call("one");
                        break;
                    case 2:
                        Call("a couple");
                        break;
                    default:
                        Call("lots");
                        break;
                }
                """;
            Assert.That(Measure(body), Is.EqualTo(1));
        }

        [Test]
        public void IfElseIfElse_OneEach()
            => Assert.That(Measure("if (c) Call(); else if (d) Call(); else Call();"), Is.EqualTo(3));

        [Test]
        public void NestedIf_AddsNesting()
            => Assert.That(Measure("if (c) { if (d) Call(); }"), Is.EqualTo(3));

        [Test]
        public void IfInsideElse_AddsNesting()
            => Assert.That(Measure("if (c) Call(); else { if (d) Call(); }"), Is.EqualTo(4));

        [Test]
        public void ElseIfBody_NestedLikeIfBody()
            => Assert.That(Measure("if (c) Call(); else if (d) { if (c) Call(); }"), Is.EqualTo(4));

        [Test]
        public void ConditionInsideIfCondition_NotNested()
            => Assert.That(Measure("if (c ? d : c) Call();"), Is.EqualTo(2));

        [Test]
        public void ConditionalInsideIfBody_Nested()
            => Assert.That(Measure("if (c) Call(d ? a : b);"), Is.EqualTo(3));

        [Test]
        public void SequenceOfSameOperators_CountsOnce()
            => Assert.That(Measure("if (c && d && c) Call();"), Is.EqualTo(2));

        [Test]
        public void MixedOperators_CountEachSequence()
            => Assert.That(Measure("if (c && d || c && d) Call();"), Is.EqualTo(4));

        [Test]
        public void ParenthesizedSameOperator_ContinuesSequence()
            => Assert.That(Measure("if ((c && d) && c) Call();"), Is.EqualTo(2));

        [Test]
        public void NegatedGroup_StartsNewSequence()
            => Assert.That(Measure("if (c && !(d && c)) Call();"), Is.EqualTo(3));

        [Test]
        public void PatternCombinators_CountLikeLogicalOperators()
            => Assert.That(Measure("if (a is 1 or 2 or 3 && b is > 0 and < 10) Call();"), Is.EqualTo(4));

        [Test]
        public void NullOperators_NotCounted()
            => Assert.That(Measure("Call(o ?? o?.ToString());"), Is.EqualTo(0));

        [Test]
        public void Loops_AddNesting()
        {
            const string body = """
                while (c)
                {
                    foreach (var (x, y) in pairs)
                    {
                        do Call(); while (d);
                    }
                }
                """;
            Assert.That(Measure(body), Is.EqualTo(6));
        }

        [Test]
        public void Catch_AddsNesting_TryAndFinallyNot()
        {
            const string body = """
                if (c)
                {
                    try { Call(); }
                    catch (System.Exception) { if (d) Call(); }
                    finally { Call(); }
                }
                """;
            Assert.That(Measure(body), Is.EqualTo(6));
        }

        [Test]
        public void SwitchExpression_CountsOnceWithNesting()
            => Assert.That(Measure("if (c) Call(a switch { 1 => 1, 2 => 2, _ => 0 });"), Is.EqualTo(3));

        [Test]
        public void Lambda_AddsNesting()
            => Assert.That(Measure("System.Action f = () => { if (c) Call(); };"), Is.EqualTo(2));

        [Test]
        public void DirectRecursion_CountsOnce()
        {
            const string source = """
                class Sample
                {
                    int Factorial(int n)
                        => n <= 1 ? 1 : n * Factorial(n - 1);
                }
                """;
            Assert.That(TestCode.Function(source, "Sample.Factorial", MetricKeys.Cognitive), Is.EqualTo(2));
        }

        [Test]
        public void OverloadWithOtherArity_NotRecursion()
        {
            const string source = """
                class Sample
                {
                    void Draw(int a)
                        => Draw(a, 0);

                    void Draw(int a, int b) { }
                }
                """;
            Assert.That(TestCode.Measure(source).Functions[0].Values[MetricKeys.Cognitive], Is.EqualTo(0));
        }

        [Test]
        public void LocalFunction_CountsSeparately()
        {
            const string source = """
                class Sample
                {
                    void Method(bool c)
                    {
                        if (c) Local();

                        void Local()
                        {
                            if (c) { if (c) return; }
                        }
                    }
                }
                """;
            Assert.That(TestCode.Function(source, "Sample.Method", MetricKeys.Cognitive), Is.EqualTo(1));
            Assert.That(TestCode.Function(source, "Sample.Method.Local", MetricKeys.Cognitive), Is.EqualTo(3));
        }
    }
}
