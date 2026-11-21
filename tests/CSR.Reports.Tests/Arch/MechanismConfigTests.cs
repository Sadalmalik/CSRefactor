using CSR.Reports.Config;

namespace CSR.Reports.Tests.Arch
{
    public class MechanismConfigTests
    {
        [Test]
        public void Project_MechanismsAndLoggerBypass()
        {
            const string json = """
                {
                    "logger": { "methods": ["Log.Error", "Log.Warning"], "bypass": ["Debug.Log*"] },
                    "mechanisms": {
                        // пул проекта
                        "pool": { "use": "ObjectPool<T>", "fields": ["Stack<*>"], "methods": ["Rent*", "Return*"] },
                        "timer": { "use": "Timers.Schedule", "creates": ["Timer"], "excludePaths": ["*/Timers/*"] },
                    },
                }
                """;
            var project = ProjectConfig.Parse(json);
            Assert.That(project.LoggerMethods, Is.EqualTo(new[] { "Log.Error", "Log.Warning" }));
            var names = project.Mechanisms.Select(mechanism => mechanism.Name);
            Assert.That(names, Is.EqualTo(new[] { "pool", "timer", "logger" }));

            var pool = project.Mechanisms[0];
            Assert.That(pool.Use, Is.EqualTo("ObjectPool<T>"));
            Assert.That(pool.Fields, Is.EqualTo(new[] { "Stack<*>" }));
            Assert.That(pool.Methods, Is.EqualTo(new[] { "Rent*", "Return*" }));
            Assert.That(pool.Types, Is.Empty);
            Assert.That(project.Mechanisms[1].ExcludePaths, Is.EqualTo(new[] { "*/Timers/*" }));

            var logger = project.Mechanisms[2];
            Assert.That(logger.Use, Is.EqualTo("Log.Error, Log.Warning"));
            Assert.That(logger.Calls, Is.EqualTo(new[] { "Debug.Log*" }));
        }

        [Test]
        public void Project_WithoutMechanisms_Empty()
            => Assert.That(ProjectConfig.Parse("""{ "logger": { "methods": ["Log"] } }""").Mechanisms, Is.Empty);

        [TestCase("""{ "mechanisms": { "pool": { "types": ["*Pool"] } } }""",
            "mechanisms.pool: не задан use - чем заменить повтор")]
        [TestCase("""{ "mechanisms": { "pool": { "use": "X", "excludePaths": ["*"] } } }""",
            "mechanisms.pool: не задано, как распознать повтор (types, fields, methods, calls, creates)")]
        [TestCase("""{ "mechanisms": { "pool": { "use": "X", "nope": [] } } }""",
            "mechanisms.pool: неизвестный ключ 'nope'")]
        [TestCase("""{ "logger": { "bypass": ["Debug.Log"] } }""",
            "logger.bypass без logger.methods: нечем заменить вызовы")]
        [TestCase("""
            { "logger": { "methods": ["Log"], "bypass": ["Debug.Log"] },
              "mechanisms": { "logger": { "use": "Log", "calls": ["Print"] } } }
            """, "logger.bypass и mechanisms.logger заданы оба: оставьте один")]
        [TestCase("""{ "logger": { "nope": [] } }""", "logger: неизвестный ключ 'nope'")]
        public void Project_InvalidMechanism_Throws(string json, string message)
        {
            var exception = Assert.Throws<ConfigException>(() => ProjectConfig.Parse(json));
            Assert.That(exception!.Message, Is.EqualTo(message));
        }
    }
}
