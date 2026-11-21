using CSR.Core.Metrics;

namespace CSR.Core.Tests.Metrics
{
    public class CrapTests
    {
        private const string Source = """
            namespace Space
            {
                class Outer
                {
                    class Inner
                    {
                        int Method(bool a, bool b)
                        {
                            if (a)
                                return 1;
                            if (b)
                                return 2;
                            return 3;
                        }

                        int Value => 1;
                    }
                }
            }
            """;

        [TestCase(5, 0.0, 30.0)]
        [TestCase(5, 100.0, 5.0)]
        [TestCase(5, 50.0, 8.125)]
        [TestCase(1, 0.0, 2.0)]
        [TestCase(10, 25.0, 52.1875)]
        public void Score_CrapperFormula(int complexity, double coverage, double expected)
            => Assert.That(Crap.Score(complexity, coverage), Is.EqualTo(expected).Within(1e-9));

        [Test]
        public void FileCoverage_PercentOverLineRange()
        {
            var coverage = new FileCoverage();
            coverage.Add(10, true);
            coverage.Add(10, false);
            coverage.Add(11, true);
            coverage.Add(20, false);

            Assert.That(coverage.GetPercent(10, 11), Is.EqualTo(100.0 * 2 / 3).Within(1e-9));
            Assert.That(coverage.GetPercent(10, 20), Is.EqualTo(50.0));
            Assert.That(coverage.GetPercent(30, 40), Is.EqualTo(0.0));
        }

        [Test]
        public void Apply_SetsCoverageAndCrap_MissingFileIsZero()
        {
            var file = TestCode.Measure(Source);
            var coverage = new FileCoverage();
            coverage.Add(9, true);
            coverage.Add(10, true);
            coverage.Add(11, true);
            coverage.Add(12, false);
            coverage.Add(13, false);
            Crap.Apply(file, coverage);

            var method = file.Functions[0];
            Assert.That(method.Coverage, Is.EqualTo(60.0));
            Assert.That(method.Crap, Is.EqualTo(3.576));

            var other = TestCode.Measure(Source);
            Crap.Apply(other, null);
            Assert.That(other.Functions[0].Coverage, Is.EqualTo(0.0));
            Assert.That(other.Functions[0].Crap, Is.EqualTo(12.0));
        }

        [Test]
        public void OwnerAndMember_SplitTypesAndMember()
        {
            var functions = TestCode.Measure(Source).Functions;
            Assert.That(functions[0].Owner, Is.EqualTo("Outer.Inner"));
            Assert.That(functions[0].Member, Is.EqualTo("Method"));
            Assert.That(functions[1].Member, Is.EqualTo("Value.get"));
            Assert.That(functions[0].Namespace, Is.EqualTo("Space"));
        }
    }
}
