using CSR.Core.Checks;
using CSR.Core.Style.Naming;
using static CSR.Core.Tests.Style.StyleTestCode;

namespace CSR.Core.Tests.Style
{
    // Требования naming из записей конфига: маски, префиксы
    public class NamingRequirementTests
    {
        [Test]
        public void Masks_ForbiddenFirstThenAllowed()
        {
            const string source = """
                public class Sample
                {
                    public event Action OnBeforeSave;
                    public event Action OnWillSave;
                    public event Action Saved;
                    public Action OnCallback;
                }
                """;
            var rule = Rule(new()
            {
                ["symbols"] = Fields(new() { ["kinds"] = new[] { "event" } }),
                ["allowed"] = new[] { "OnBefore*", "OnAfter*" },
                ["forbidden"] = new[] { "OnWill*" },
            });

            var messages = RunRules(source, rule).Select(finding => finding.Message);

            Assert.That(messages, Is.EqualTo(new[]
            {
                "событие 'OnWillSave': запрещенная форма 'OnWill*'",
                "событие 'Saved': допустимо OnBefore*, OnAfter*",
            }));
        }

        [Test]
        public void Prefix_ForbiddenBeforeUpper()
        {
            const string source = """
                public delegate void OnChanged();
                public delegate void Onboarding();
                public delegate void Changed();
                """;
            var rule = Rule(new()
            {
                ["symbols"] = Fields(new() { ["kinds"] = new[] { "delegate" } }),
                ["forbiddenPrefixes"] = new[] { "On" },
            });

            var finding = RunRules(source, rule).Single();

            Assert.That(finding.Message, Is.EqualTo("delegate 'OnChanged': без префикса 'On'"));
        }

        [Test]
        public void Prefix_WordRequiredAndAlone()
        {
            const string source = """
                public interface Storage { }
                public interface IStorage { }
                public class Box<T, TValue, Item> { }
                """;
            var interfaces = Rule(new()
            {
                ["symbols"] = Fields(new() { ["kinds"] = new[] { "interface" } }),
                ["prefix"] = "I",
                ["prefixStyle"] = "word",
            });
            var typeParameters = Rule(new()
            {
                ["symbols"] = Fields(new() { ["kinds"] = new[] { "typeParameter" } }),
                ["prefix"] = "T",
                ["prefixStyle"] = "word",
                ["prefixAlone"] = true,
            });

            var messages = RunRules(source, interfaces, typeParameters).Select(finding => finding.Message);

            Assert.That(messages, Is.EqualTo(new[] { "интерфейс 'Storage': префикс 'I'", "параметр-тип 'Item': префикс 'T'" }));
        }

        [Test]
        public void Words_NegationWithLabelAndNamespaceParts()
        {
            const string source = """
                namespace Game.EditorTools
                {
                    public class Sample
                    {
                        private bool m_IsNotReady;
                        private bool m_IsDisabled;
                        private bool m_IsDisplayed;
                    }
                }
                """;
            var bools = Rule(new()
            {
                ["symbols"] = Fields(new() { ["types"] = new[] { "bool" } }),
                ["forbiddenWords"] = new[] { "Not" },
                ["negationPrefixes"] = new[] { "Dis" },
                ["ignoreWords"] = new[] { "Displayed" },
                ["negation"] = true,
                ["label"] = "bool",
            });
            var namespaces = Rule(new()
            {
                ["symbols"] = Fields(new() { ["kinds"] = new[] { "namespace" } }),
                ["forbiddenWords"] = new[] { "Editor" },
            });

            var messages = RunRules(source, bools, namespaces).Select(finding => finding.Message);

            Assert.That(messages, Is.EqualTo(new[]
            {
                "пространство 'Game.EditorTools': слово 'Editor'",
                "bool 'm_IsNotReady': отрицание 'Not' - имя по состоянию",
                "bool 'm_IsDisabled': отрицание 'Disabled' - имя по состоянию",
            }));
        }

        public static List<Finding> RunRules(string source, params NamingRule[] rules)
            => Run("naming", source, settings => settings.Set("rules", rules.Cast<object>().ToList()));

        public static NamingRule Rule(Dictionary<string, object> fields)
            => NamingRule.Parse(Fields(fields));

        public static Record Fields(Dictionary<string, object> fields)
            => new(fields);
    }
}
