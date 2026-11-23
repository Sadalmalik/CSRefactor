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

        [Test]
        public void Systems_InventoryInJson()
        {
            using var folder = new TempFolder();
            folder.Write("Rules.cs", """
                using System.Collections.Generic;
                public abstract class Rule { public abstract int Apply(float value); }
                public class FirstRule : Rule { public override int Apply(float value) => 1; }
                public class SecondRule : Rule { public override int Apply(float value) => 2; }
                public class RuleSet { private List<Rule> m_Rules; }
                """);
            var result = Cli.Run("similar", folder.Path, "--kinds", "system");

            Assert.That(result.ExitCode, Is.EqualTo(0), result.Error);
            using var document = JsonDocument.Parse(result.Output);
            var system = document.RootElement.GetProperty("inventory")[0];
            Assert.That(system.GetProperty("host").GetProperty("name").GetString(), Is.EqualTo("RuleSet"));
            Assert.That(system.GetProperty("extensionPoint").GetProperty("name").GetString(), Is.EqualTo("Rule"));
            Assert.That(system.GetProperty("holder").GetString(), Is.EqualTo("collection"));
            Assert.That(system.GetProperty("variantCount").GetInt32(), Is.EqualTo(2));
            Assert.That(system.GetProperty("hostsOfExtensionPoint").GetInt32(), Is.EqualTo(1));
        }

        [Test]
        public void Kinds_UnknownKind_ExitTwo()
        {
            using var folder = new TempFolder();
            folder.Write("Totals.cs", Totals);
            var result = Cli.Run("similar", folder.Path, "--kinds", "member,classes");
            Assert.That(result.ExitCode, Is.EqualTo(2));
            Assert.That(result.Error, Does.Contain("неизвестный вид 'classes'"));
        }

        [Test]
        public void ConfigSynonyms_ParameterSetPair()
        {
            using var folder = new TempFolder();
            folder.Write(
                "Projectiles.cs",
                "class Projectiles { void Launch(Vector3 position, Vector3 direction, float speed) { } }");
            folder.Write("Units.cs", "class Units { void Move(Vector3 pos, Vector3 dir, float speed, int frame) { } }");
            var config = folder.Write("similar.jsonc", """
                {
                  "kinds": ["parameters"],
                  "synonyms": { "Pos": "Position", "dir": "direction" },
                  "weights": { "bodies": 3 },
                  "minCoreMembers": 0,
                  "typeThreshold": 0.9
                }
                """);
            var result = Cli.Run("similar", folder.Path, "--config", config, "--format", "md");

            Assert.That(result.ExitCode, Is.EqualTo(0), result.Error);
            Assert.That(result.Output, Does.Contain("## 1. наборы параметров: мест 2, пар 1, оценка 0.75"));
            Assert.That(result.Output, Does.Contain("Общее: `Vector3 position`, `Vector3 direction`, `float speed`"));
            Assert.That(result.Output, Does.Contain("  - своё: `int frame`"));
        }

        [Test]
        public void Changed_TypeAgainstCommitted()
        {
            const string rule = """
                public class FirstRule : RuleBase
                {
                    private readonly int m_Depth = 2;
                    public override string Name => "first";
                    public override void Apply(Context context)
                    {
                        foreach (var node in context.Nodes)
                        {
                            if (node.Depth > 2)
                                context.Report(node, "first: " + node.Text);
                        }
                    }
                    private static bool Matches(string text) => text.StartsWith("a");
                }
                """;
            using var folder = new TempFolder();
            folder.Write("FirstRule.cs", rule);
            Cli.InitRepository(folder.Path);
            folder.Write("SecondRule.cs", rule.Replace("FirstRule", "SecondRule").Replace("\"first", "\"second"));
            var result = Cli.Run("similar", folder.Path, "--changed", "--kinds", "type", "--format", "md");

            Assert.That(result.ExitCode, Is.EqualTo(0), result.Error);
            Assert.That(result.Output, Does.Match("## типы: `[^`]*SecondRule.cs:1-14` `SecondRule`"));
            Assert.That(result.Output, Does.Match("- 1.00: `[^`]*FirstRule.cs:1-14` `FirstRule`"));
        }
    }
}
