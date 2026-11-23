using CSR.Core.Similar;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Tests.Similar
{
    public class SimilarSearchTests
    {
        private const string Sum = """
            public class Totals
            {
                public int Sum(int[] values)
                {
                    int total = 0;
                    foreach (var value in values)
                    {
                        if (value > 10 && this.IsValid(value))
                            total += Math.Max(value * 2, 1);
                    }

                    return total;
                }
            }
            """;

        private const string Count = """
            public class Counter
            {
                public int Count(int[] items)
                {
                    int result = 5;
                    foreach (var item in items)
                    {
                        if (item > 3 && IsValid(item))
                            result += Math.Max(item * 7, 9);
                    }

                    return result;
                }
            }
            """;

        private const string Describe = """
            public class Report
            {
                public string Describe(List<string> names)
                {
                    var builder = new StringBuilder();
                    for (int i = 0; i < names.Count; i++)
                        builder.AppendLine(names[i].Trim());

                    return builder.ToString();
                }
            }
            """;

        [Test]
        public void SameShape_DifferentNamesAndLiterals_ScoreOne()
        {
            var pair = Search(In("Totals.cs", Sum), In("Counter.cs", Count)).Pairs.Single();
            Assert.That(pair.Score, Is.EqualTo(1.0));
            Assert.That(pair.Left.Member, Is.EqualTo("Counter.Count"));
            Assert.That(pair.Right.Member, Is.EqualTo("Totals.Sum"));
            Assert.That((pair.Right.StartLine, pair.Right.EndLine), Is.EqualTo((3, 13)));
        }

        [Test]
        public void DifferentShape_BelowThreshold()
            => Assert.That(Search(In("Totals.cs", Sum), In("Report.cs", Describe)).Pairs, Is.Empty);

        [Test]
        public void CalledMethodName_IsKept()
        {
            var other = Count.Replace("Math.Max", "Math.Min");
            var units = Collect(In("Totals.cs", Sum), In("Counter.cs", other));
            double score = SimilarFinder.GetJaccard(units[0].Fingerprints, units[1].Fingerprints);
            // имя вызова входит во все поддеревья над вызовом: пара падает ниже порога
            Assert.That(score, Is.LessThan(new SimilarOptions().Threshold));
        }

        [Test]
        public void PartialClone_FoundAsMergedFragment()
        {
            const string common = """
                        var path = Path.Combine(root, name);
                        if (!File.Exists(path))
                            throw new InvalidOperationException("missing " + name);
                        var text = File.ReadAllText(path);
                        var lines = text.Split('\n').Where(line => line.Length > 0).ToList();
                        var comments = lines.Count(line => line.StartsWith("#"));
                """;
            var load = $$"""
                public class Loader
                {
                    public int Load(string root, string name)
                    {
                {{common}}
                        int sum = 0;
                        foreach (var line in lines)
                            sum += int.Parse(line.Trim());
                        return sum;
                    }
                }
                """;
            var import = $$"""
                public class Importer
                {
                    public Dictionary<string, string> Import(string folder, string file)
                    {
                {{common}}
                        var result = new Dictionary<string, string>();
                        switch (lines.Count)
                        {
                            case 0:
                                Log.Warning("empty");
                                break;
                            default:
                                result[lines[0]] = string.Join(",", lines.Skip(1));
                                break;
                        }

                        return result;
                    }
                }
                """;
            var result = Search(In("Importer.cs", import), In("Loader.cs", load));
            var pair = result.Pairs.Single();
            Assert.That(pair.Left.Kind, Is.EqualTo(UnitKind.Fragment));
            Assert.That(pair.Windows, Is.EqualTo(2));
            Assert.That((pair.Left.StartLine, pair.Left.EndLine), Is.EqualTo((5, 10)));
            Assert.That((pair.Right.StartLine, pair.Right.EndLine), Is.EqualTo((5, 10)));
            Assert.That(pair.Left.Member, Is.EqualTo("Importer.Import"));
        }

        [Test]
        public void WholeMemberPair_HidesItsFragments()
        {
            var units = Collect(In("Totals.cs", Sum), In("Counter.cs", Count));
            var result = SimilarSearch.Run(
                units,
                new SimilarOptions { FragmentStatements = 2, MinLines = 1, MinNodes = 1, MinShapes = 1 });
            Assert.That(result.Pairs.Select(pair => pair.Left.Kind), Is.EqualTo(new[] { UnitKind.Member }));
        }

        [Test]
        public void ThisAndNameof_Normalized()
        {
            const string left = """
                public class Left
                {
                    public void Store(int value)
                    {
                        this.m_Count = value + nameof(value).Length;
                        this.m_Total += this.m_Count * 3;
                        Notify(this.m_Count, this.m_Total);
                        Save();
                    }
                }
                """;
            const string right = """
                public class Right
                {
                    public void Keep(int amount)
                    {
                        m_Size = amount + "x".Length;
                        m_Sum += m_Size * 4;
                        Notify(m_Size, m_Sum);
                        Save();
                    }
                }
                """;
            var units = Collect(In("Left.cs", left), In("Right.cs", right));
            Assert.That(SimilarFinder.GetJaccard(units[0].Fingerprints, units[1].Fingerprints), Is.EqualTo(1.0));
        }

        [Test]
        public void LambdaInsideMethod_NotPairedWithIt()
        {
            const string source = """
                public class Host
                {
                    public void Run(List<int> values)
                    {
                        values.ForEach(value =>
                        {
                            if (value > 10 && IsValid(value))
                                Total += Math.Max(value * 2, 1);
                            Log.Info(value.ToString());
                        });
                    }
                }
                """;
            var options = new SimilarOptions { Threshold = 0.5, MinLines = 1, MinNodes = 1, MinShapes = 1 };
            var units = MemberUnits.Collect(Parse("Host.cs", source), options);
            Assert.That(units.Select(unit => unit.Member), Is.EqualTo(new[] { "Host.Run", "Host.Run (лямбда)" }));
            Assert.That(SimilarSearch.Run(units, options).Pairs, Is.Empty);
        }

        [Test]
        public void ThreeCopies_OneCluster()
        {
            var third = Sum.Replace("Totals", "Third").Replace("Sum(", "Add(");
            var result = Search(In("Totals.cs", Sum), In("Counter.cs", Count), In("Third.cs", third));
            var cluster = result.Clusters.Single();
            Assert.That(
                cluster.Units.Select(unit => unit.Member),
                Is.EqualTo(new[] { "Counter.Count", "Third.Add", "Totals.Sum" }));
            Assert.That(cluster.PairCount, Is.EqualTo(3));
            Assert.That(cluster.MinScore, Is.EqualTo(1.0));
        }

        [Test]
        public void Probe_OnlyPairsWithProbedUnit()
        {
            var third = Sum.Replace("Totals", "Third").Replace("Sum(", "Add(");
            var units = Collect(In("Totals.cs", Sum), In("Counter.cs", Count), In("Third.cs", third));
            var result = SimilarSearch.Run(units, new SimilarOptions(), unit => unit.File == "Third.cs");
            Assert.That(result.Pairs.Select(pair => pair.Left.File), Is.EqualTo(new[] { "Third.cs", "Third.cs" }));
            Assert.That(result.Pairs.Select(pair => pair.Right.File), Is.EquivalentTo(new[] { "Counter.cs", "Totals.cs" }));
        }

        [Test]
        public void TrivialAssignments_FilteredByMinShapes()
        {
            const string first = """
                public class First
                {
                    public First(int a, int b, int c, int d, int e)
                    {
                        m_A = a;
                        m_B = b;
                        m_C = c;
                        m_D = d;
                        m_E = e;
                    }
                }
                """;
            var second = first.Replace("First", "Second");
            Assert.That(Search(In("First.cs", first), In("Second.cs", second)).Pairs, Is.Empty);

            var options = new SimilarOptions { MinShapes = 0 };
            var units = MemberUnits.Collect(Parse("First.cs", first), options)
                .Concat(MemberUnits.Collect(Parse("Second.cs", second), options))
                .ToList();
            Assert.That(SimilarSearch.Run(units, options).Pairs, Is.Not.Empty);
        }

        private static SimilarResult Search(params SourceFile[] files)
            => SimilarSearch.Run(Collect(files), new SimilarOptions());

        private static List<SimilarUnit> Collect(params SourceFile[] files)
        {
            var options = new SimilarOptions();
            return files.SelectMany(file => MemberUnits.Collect(Parse(file.Path, file.Text), options)).ToList();
        }

        private static SourceFile In(string path, string text)
            => new(path, text);

        private static Microsoft.CodeAnalysis.SyntaxTree Parse(string path, string source)
            => CSharpSyntaxTree.ParseText(source, path: path);

        private class SourceFile
        {
            public SourceFile(string path, string text)
            {
                Path = path;
                Text = text;
            }

            public string Path { get; }

            public string Text { get; }
        }
    }
}
