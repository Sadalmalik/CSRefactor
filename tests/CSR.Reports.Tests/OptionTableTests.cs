using CSR.Core.Similar;
using CSR.Reports.Config;

namespace CSR.Reports.Tests
{
    // Таблица ключей конфига: одно правило значения для .jsonc и командной строки
    public class OptionTableTests
    {
        [Test]
        public void Similar_ConfigValues()
        {
            var options = SimilarConfig.Parse("""
                {
                  "kinds": ["member", "type"], "threshold": 0.9, "minLines": 0,
                  "weights": { "bodies": 3 }, "synonyms": { "Pos": "Position" },
                }
                """);

            Assert.That(options.Kinds, Is.EquivalentTo(new[] { UnitKind.Member, UnitKind.Type }));
            Assert.That(options.Threshold, Is.EqualTo(0.9));
            Assert.That(options.MinLines, Is.EqualTo(0));
            Assert.That(options.Weights.Bodies, Is.EqualTo(3));
            Assert.That(options.Weights.Bases, Is.EqualTo(1));
            Assert.That(options.Synonyms["pos"], Is.EqualTo("position"));
        }

        [TestCase("""{ "minLines": -1 }""", "minLines: ожидается неотрицательное целое")]
        [TestCase("""{ "minLines": 1.5 }""", "minLines: ожидается неотрицательное целое")]
        [TestCase("""{ "threshold": 1.5 }""", "threshold: ожидается число от 0 до 1")]
        [TestCase("""{ "weights": { "bodies": -1 } }""", "weights.bodies: ожидается неотрицательное число")]
        [TestCase("""{ "weights": { "nope": 1 } }""", "weights: неизвестный ключ 'nope'")]
        [TestCase("""{ "kinds": ["classes"] }""", "kinds: неизвестный вид 'classes'; допустимо")]
        [TestCase("""{ "limit": 1 }""", "неизвестный ключ 'limit'")]
        [TestCase("""[]""", "similar: ожидается объект")]
        public void Similar_InvalidValue_ConfigError(string json, string message)
        {
            var exception = Assert.Throws<ConfigException>(() => SimilarConfig.Parse(json));
            Assert.That(exception!.Message, Does.StartWith(message));
        }

        [TestCase("""{ "top": 0 }""", "top: ожидается положительное целое")]
        [TestCase("""{ "godTcc": 2 }""", "godTcc: ожидается число от 0 до 1")]
        public void Structure_InvalidValue_ConfigError(string json, string message)
        {
            var exception = Assert.Throws<ConfigException>(() => StructureConfig.Parse(json));
            Assert.That(exception!.Message, Is.EqualTo(message));
        }

        // Опции командной строки - те же виды: доля с запятой, целое >= 0, текст ошибки с заданным значением
        [Test]
        public void CommandLine_SameRules()
        {
            var options = new SimilarOptions();
            var values = new Dictionary<string, string> { ["--threshold"] = "0,9", ["--kinds"] = "member, type" };
            SimilarConfig.Table.ReadOptions(name => values.GetValueOrDefault(name), options);

            Assert.That(options.Threshold, Is.EqualTo(0.9));
            Assert.That(options.Kinds, Is.EquivalentTo(new[] { UnitKind.Member, UnitKind.Type }));

            var exception = Assert.Throws<ConfigException>(() => SimilarConfig.Table.ReadOptions(
                name => name == "--min-lines" ? "-1" : null,
                options));
            Assert.That(exception!.Message, Is.EqualTo("--min-lines: ожидается неотрицательное целое, задано '-1'"));
        }

        [Test]
        public void Section_KeysInSameOptions()
        {
            var table = new OptionTable<SimilarOptions>()
                .AddSection("limits", new OptionTable<SimilarOptions>()
                    .Add("lines", OptionKinds.Count, (options, value) => options.MinLines = value), options => options);
            var target = new SimilarOptions();

            table.ReadRoot("""{ "limits": { "lines": 7 } }""", "test", target);

            Assert.That(target.MinLines, Is.EqualTo(7));
            var exception = Assert.Throws<ConfigException>(
                () => table.ReadRoot("""{ "limits": { "rows": 1 } }""", "test", target));
            Assert.That(exception!.Message, Is.EqualTo("limits: неизвестный ключ 'rows'"));
        }
    }
}
