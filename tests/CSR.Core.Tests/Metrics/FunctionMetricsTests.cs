using CSR.Core.Metrics;

namespace CSR.Core.Tests.Metrics
{
    public class FunctionMetricsTests
    {
#region Nesting

        [Test]
        public void Nesting_FlatBody_IsZero()
            => Assert.That(TestCode.Method("Call();", MetricKeys.Nesting), Is.EqualTo(0));

        [Test]
        public void Nesting_StatementInsideIf_IsOne()
            => Assert.That(TestCode.Method("if (c) Call();", MetricKeys.Nesting), Is.EqualTo(1));

        [Test]
        public void Nesting_ElseIfChain_SameLevel()
        {
            const string body = "if (c) Call(); else if (d) Call(); else if (c) Call(); else Call();";
            Assert.That(TestCode.Method(body, MetricKeys.Nesting), Is.EqualTo(1));
        }

        [Test]
        public void Nesting_CountsLoopsTryUsingLockSwitch()
        {
            const string body = """
                foreach (int item in items)
                {
                    try
                    {
                        using (new System.IO.MemoryStream())
                        {
                            lock (o)
                            {
                                switch (item)
                                {
                                    case 1:
                                        Call();
                                        break;
                                }
                            }
                        }
                    }
                    finally { }
                }
                """;
            Assert.That(TestCode.Method(body, MetricKeys.Nesting), Is.EqualTo(5));
        }

        [Test]
        public void Nesting_LambdaBody_CountsEnclosingStatements()
        {
            const string body = "if (c) { System.Action f = () => { if (d) Call(); }; }";
            Assert.That(TestCode.Method(body, MetricKeys.Nesting), Is.EqualTo(2));
        }

        [Test]
        public void Nesting_ExpressionBody_IsZero()
        {
            const string source = "class Sample { int Method(bool c) => c ? 1 : 0; }";
            Assert.That(TestCode.Function(source, "Sample.Method", MetricKeys.Nesting), Is.EqualTo(0));
        }

#endregion

#region Lines

        [Test]
        public void Lines_PhysicalAndCode()
        {
            const string source = """
                class Sample
                {
                    [System.Obsolete]
                    void Method()
                    {
                        // комментарий

                        Call();
                #if UNITY_EDITOR
                        Call();
                #else
                        Call();
                #endif
                    }

                    void Call() { }
                }
                """;
            MetricItem method = TestCode.Function(source, "Sample.Method");
            Assert.That(method.Line, Is.EqualTo(3));
            Assert.That(method.EndLine, Is.EqualTo(14));
            Assert.That(method.Values[MetricKeys.Lines], Is.EqualTo(12));
            Assert.That(method.Values[MetricKeys.CodeLines], Is.EqualTo(6));
        }

        [Test]
        public void CodeLines_MultilineStringCountsEachLine()
        {
            const string source = """"
                class Sample
                {
                    string Method()
                        => """
                            a

                            b
                            """;
                }
                """";
            Assert.That(TestCode.Function(source, "Sample.Method", MetricKeys.CodeLines), Is.EqualTo(6));
        }

#endregion

#region Parameters and names

        [Test]
        public void Parameters_CountsAll()
        {
            const string source = "class Sample { void Method(int a, ref int b, params int[] c) { } }";
            Assert.That(TestCode.Function(source, "Sample.Method", MetricKeys.Parameters), Is.EqualTo(3));
        }

        [Test]
        public void Parameters_AccessorIsZero()
        {
            const string source = "class Sample { int Value { get; set; } }";
            Assert.That(TestCode.Function(source, "Sample.Value.set", MetricKeys.Parameters), Is.EqualTo(0));
        }

        [Test]
        public void Names_AllFunctionKinds()
        {
            const string source = """
                namespace Outer.Inner
                {
                    class Sample<T>
                    {
                        static Sample() { }
                        Sample(int a) { }
                        ~Sample() { }
                        int this[int i] { get => i; set { } }
                        event System.Action Changed { add { } remove { } }
                        public static Sample<T> operator |(Sample<T> a, Sample<T> b) => a;
                        public static implicit operator int(Sample<T> a) => 0;

                        class Nested
                        {
                            void Method()
                            {
                                void Local() { }
                            }
                        }
                    }
                }
                """;
            FileMetrics file = TestCode.Measure(source);
            string[] names = file.Functions.Select(function => $"{function.Kind} {function.Name}").ToArray();
            Assert.That(names, Is.EqualTo(new[]
            {
                "constructor Sample<T>.Sample",
                "constructor Sample<T>.Sample",
                "destructor Sample<T>.~Sample",
                "accessor Sample<T>.this[].get",
                "accessor Sample<T>.this[].set",
                "accessor Sample<T>.Changed.add",
                "accessor Sample<T>.Changed.remove",
                "operator Sample<T>.operator |",
                "operator Sample<T>.implicit operator int",
                "method Sample<T>.Nested.Method",
                "local-function Sample<T>.Nested.Method.Local",
            }));
            Assert.That(file.Functions.All(function => function.Namespace == "Outer.Inner"));
        }

#endregion
    }
}
