using CSR.Reports.Config;

namespace CSR.Reports.Tests
{
    // Записи правил naming в конфиге: ошибки разбора - с путем записи
    public class NamingConfigTests
    {
        [TestCase("""{ "checks": { "naming": { "rules": {} } } }""", "naming.rules: ожидается массив записей")]
        [TestCase("""{ "checks": { "naming": { "rules": [1] } } }""", "naming.rules[0]: ожидается объект")]
        [TestCase("""{ "checks": { "naming": { "rules": [{ "nope": 1 }] } } }""", "naming.rules[0]: неизвестное поле 'nope'")]
        [TestCase(
            """{ "checks": { "naming": { "rules": [{ "rule": "N1" }] } } }""",
            "naming.rules[0]: в правиле нет требования к имени")]
        [TestCase(
            """{ "checks": { "naming": { "rules": [{ "symbols": { "kind": ["field"] } }] } } }""",
            "naming.rules[0]: symbols: неизвестное поле 'kind'")]
        public void Rules_InvalidRecord_ConfigErrorWithPath(string json, string message)
        {
            var exception = Assert.Throws<ConfigException>(() => StyleConfig.Parse(json));

            Assert.That(exception!.Message, Is.EqualTo(message));
        }

        [Test]
        public void Rules_Empty_NoRules()
        {
            var options = StyleConfig.Parse("""{ "checks": { "naming": { "severity": "warning", "rules": [] } } }""");

            Assert.That(options.Checks.Single().Settings.GetRecords<object>("rules"), Is.Empty);
        }
    }
}
