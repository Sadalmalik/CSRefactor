using CSR.Workspace.Loading;
using CSR.Workspace.Refactor;

namespace CSR.Workspace.Tests
{
    // csr extract: извлечение метода по строкам на своей копии решения с файлом Game/Calc.cs
    public class ExtractTests
    {
        private const string Calc = """
            namespace Sample.Items
            {
                public class Calc
                {
                    private int m_Bonus = 2;

                    public int Score(int a, int b)
                    {
                        var sum = a + b;
                        var doubled = sum * 2;
                        Console.WriteLine(doubled);
                        return doubled + m_Bonus;
                    }

                    public void Log(int a)
                    {
                        Console.WriteLine(a + m_Bonus);
                    }

                    public int Range(int a)
                    {
                        var low = 0;
                        var high = 0;
                        low = a - 1;
                        high = a + 1;
                        return high - low;
                    }

                    public int Counter(int a)
                    {
                        var total = a;
                        total += 5;
                        return total;
                    }

                    public int Early(int a)
                    {
                        if (a > 0)
                            return 1;
                        return 0;
                    }

                    public string Label(int a)
                    {
                        var text = a.ToString();
                        return text;
                    }
                }
            }

            """;

        private SampleSolution m_Sample = null!;
        private LoadedSolution m_Solution = null!;

        [OneTimeSetUp]
        public async Task Load()
        {
            m_Sample = SampleSolution.Create();
            File.WriteAllText(m_Sample.GetPath("Game/Calc.cs"), Calc);
            m_Solution = await m_Sample.LoadAsync();
        }

        [OneTimeTearDown]
        public void Unload()
        {
            m_Solution.Dispose();
            m_Sample.Dispose();
        }

        [Test]
        public async Task Extract_InputsAndReturnedLocal()
        {
            var plan = await PlanAsync(9, 10, "Double");

            Assert.That(plan.Refusal, Is.Null);
            var text = plan.Changes.Single().After.ToString().Replace("\r\n", "\n");
            var expected = string.Join(
                "\n",
                "            var doubled = Double(a, b);",
                "            Console.WriteLine(doubled);",
                "            return doubled + m_Bonus;",
                "        }",
                "",
                "        private static int Double(int a, int b)",
                "        {",
                "            var sum = a + b;",
                "            var doubled = sum * 2;",
                "            return doubled;",
                "        }",
                "",
                "        public void Log(int a)");
            Assert.That(text, Does.Contain(expected));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
        }

        [Test]
        public async Task Extract_InstanceMemberKeepsInstance()
        {
            var plan = await PlanAsync(17, 17, "Print");

            var text = plan.Changes.Single().After.ToString();
            Assert.That(text, Does.Contain("            Print(a);"));
            Assert.That(text, Does.Contain("        private void Print(int a)"));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
        }

        [Test]
        public async Task Extract_SeveralOutputsAsOut()
        {
            var plan = await PlanAsync(24, 25, "Bounds");

            var text = plan.Changes.Single().After.ToString();
            Assert.That(text, Does.Contain("            Bounds(a, out low, out high);"));
            Assert.That(text, Does.Contain("        private static void Bounds(int a, out int low, out int high)"));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
        }

        // Тип var-переменной Roslyn помечает как nullable: возврат - по типу инициализатора
        [Test]
        public async Task Extract_VarLocalTypeFromInitializer()
        {
            var plan = await PlanAsync(45, 45, "Text");

            var text = plan.Changes.Single().After.ToString();
            Assert.That(text, Does.Contain("        private static string Text(int a)"));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
        }

        [TestCase(32, 32, "нужен ref")]
        [TestCase(11, 12, "return в строках")]
        [TestCase(39, 40, "не целые инструкции")]
        [TestCase(6, 6, "в строках нет кода")]
        public async Task Extract_Refusals(int start, int end, string reason)
        {
            var plan = await PlanAsync(start, end, "Part");

            Assert.That(plan.Refusal, Does.Contain(reason));
            Assert.That(plan.Changes, Is.Empty);
        }

        private Task<RefactorPlan> PlanAsync(int start, int end, string name)
            => MethodExtractor.PlanAsync(m_Solution, m_Sample.GetPath("Game/Calc.cs"), start, end, name, true);
    }
}
