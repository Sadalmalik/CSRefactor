using System.Text.Json;

namespace CSR.Tests
{
    public class SimilarCommandTests
    {
        private const string Totals = """
            public class Totals
            {
                public int Sum(int[] values)
                {
                    int total = 0;
                    foreach (var value in values)
                    {
                        if (value > 10 && IsValid(value))
                            total += Math.Max(value * 2, 1);
                    }

                    return total;
                }
            }
            """;

        private static readonly string Counter = Totals.Replace("Totals", "Counter").Replace("Sum(", "Count(");

        [Test]
        public void Markdown_Cluster()
        {
            using var folder = new TempFolder();
            folder.Write("Totals.cs", Totals);
            folder.Write("Counter.cs", Counter);
            var result = Cli.Run("similar", folder.Path, "--format", "md");

            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Output, Does.Contain("## 1. члены: мест 2, пар 1, оценка 1.00"));
            Assert.That(result.Output, Does.Contain("Counter.cs:3-13` `Counter.Count`"));
            Assert.That(result.Error, Does.Contain("пар 1, кластеров 1"));
        }

        [Test]
        public void Json_ConfigExcludePaths()
        {
            using var folder = new TempFolder();
            folder.Write("Totals.cs", Totals);
            folder.Write("Tests/Counter.cs", Counter);
            folder.Write("Other.cs", Counter.Replace("Counter", "Other"));
            var config = folder.Write("similar.jsonc", """{ "threshold": 0.9, "excludePaths": ["*/Tests/*"] }""");
            var result = Cli.Run("similar", folder.Path, "--config", config);

            using var document = JsonDocument.Parse(result.Output);
            var root = document.RootElement;
            Assert.That(root.GetProperty("files").GetInt32(), Is.EqualTo(2));
            Assert.That(root.GetProperty("threshold").GetDouble(), Is.EqualTo(0.9));
            var pair = root.GetProperty("pairs")[0];
            Assert.That(pair.GetProperty("left").GetProperty("member").GetString(), Is.EqualTo("Other.Count"));
            Assert.That(pair.GetProperty("score").GetDouble(), Is.EqualTo(1.0));
        }

        [Test]
        public void Changed_NewFileAgainstCommitted()
        {
            using var folder = new TempFolder();
            folder.Write("Totals.cs", Totals);
            folder.Write("Other.cs", Counter.Replace("Counter", "Other"));
            Cli.InitRepository(folder.Path);
            folder.Write("Counter.cs", Counter);
            var result = Cli.Run("similar", folder.Path, "--changed", "--format", "md");

            Assert.That(result.ExitCode, Is.EqualTo(0));
            Assert.That(result.Output, Does.Contain("Counter.cs:3-13` `Counter.Count`"));
            Assert.That(result.Error, Does.Contain("пар 2"));
            Assert.That(result.Output, Does.Not.Contain("## " + Path.Combine(folder.Path, "Other.cs")));
        }

        [TestCase("--threshold", "1.5")]
        [TestCase("--min-lines", "-1")]
        [TestCase("--format", "xml")]
        public void InvalidArguments_ExitTwo(string option, string value)
        {
            using var folder = new TempFolder();
            folder.Write("Totals.cs", Totals);
            Assert.That(Cli.Run("similar", folder.Path, option, value).ExitCode, Is.EqualTo(2));
        }

        [Test]
        public void UnknownConfigKey_ExitTwo()
        {
            using var folder = new TempFolder();
            folder.Write("Totals.cs", Totals);
            var config = folder.Write("similar.jsonc", """{ "limit": 3 }""");
            var result = Cli.Run("similar", folder.Path, "--config", config);
            Assert.That(result.ExitCode, Is.EqualTo(2));
            Assert.That(result.Error, Does.Contain("неизвестный ключ 'limit'"));
        }

        [Test]
        public void Help()
            => Assert.That(Cli.Run("help", "similar").Output, Does.StartWith("csr similar"));
    }
}
