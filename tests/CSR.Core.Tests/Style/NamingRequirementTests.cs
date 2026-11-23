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

        [Test]
        public void Number_PluralForCollectionsAndArrays()
        {
            const string source = """
                public class Sample
                {
                    private List<int> m_Item;
                    private List<int> m_Items;
                    private int[,] m_Grid;
                    private int[] m_Status;
                    private Dictionary<int, string> m_NameData;
                }
                """;
            var rule = Rule(new()
            {
                ["symbols"] = Fields(new() { ["types"] = new[] { "List<*>", "Dictionary<*>", "*[*]" } }),
                ["number"] = "plural",
                ["pluralWords"] = new[] { "Data" },
                ["label"] = "коллекция",
            });

            var messages = RunRules(source, rule).Select(finding => finding.Message);

            Assert.That(messages, Is.EqualTo(new[]
            {
                "коллекция 'm_Item': имя во множественном числе",
                "коллекция 'm_Grid': имя во множественном числе",
                "коллекция 'm_Status': имя во множественном числе",
            }));
        }

        [Test]
        public void Namespace_RootsTypeOutsideAndDepth()
        {
            const string source = """
                public class Helper
                {
                    public class Nested { }
                }

                namespace Company.Game.Ui.Hud.Bars
                {
                    public class Bar { }
                }

                namespace Tools.Debug
                {
                    public class Probe { }
                }
                """;
            var namespaces = Rule(new()
            {
                ["symbols"] = Fields(new() { ["kinds"] = new[] { "namespace" } }),
                ["roots"] = new[] { "Company" },
                ["maxParts"] = 4,
            });
            var types = Rule(new()
            {
                ["symbols"] = Fields(new() { ["kinds"] = new[] { "class" } }),
                ["roots"] = new[] { "Company" },
                ["label"] = "тип",
            });

            var messages = RunRules(source, namespaces, types).Select(finding => finding.Message);

            Assert.That(messages, Is.EqualTo(new[]
            {
                "тип 'Helper' вне пространства имен",
                "пространство 'Company.Game.Ui.Hud.Bars': частей 5, предел 4",
                "пространство 'Tools.Debug' вне корня: Company",
            }));
        }

        // Каскад полей: константа занимает вид prefix значением "*", дальше - открытые поля без префикса, затем m_
        [Test]
        public void FieldPrefixAndCase_Cascade()
        {
            const string source = """
                public class Sample
                {
                    private const int MAX_COUNT = 3;
                    private int count;
                    private int m_total;
                    public int m_Limit;
                    public int Value { get; set; }
                    public int m_Size { get; set; }
                }
                """;
            var constants = Rule(new()
            {
                ["symbols"] = Fields(new() { ["kinds"] = new[] { "field" }, ["modifiers"] = new[] { "const" } }),
                ["case"] = "pascal",
                ["prefix"] = "*",
            });
            var publicFields = Rule(new()
            {
                ["symbols"] = Fields(new() { ["kinds"] = new[] { "field" }, ["access"] = new[] { "public" } }),
                ["prefix"] = "",
            });
            var fields = Rule(new()
            {
                ["symbols"] = Fields(new() { ["kinds"] = new[] { "field" } }),
                ["prefix"] = "m_",
                ["pascalAfterPrefix"] = true,
            });
            var properties = Rule(new()
            {
                ["symbols"] = Fields(new() { ["kinds"] = new[] { "property" } }),
                ["prefix"] = "",
            });

            var messages = RunRules(source, constants, publicFields, fields, properties).Select(finding => finding.Message);

            Assert.That(messages, Is.EqualTo(new[]
            {
                "'MAX_COUNT': константа в PascalCase - 'MaxCount'",
                "'count': закрытое поле с префиксом 'm_' - 'm_Count'",
                "'m_total': после префикса - заглавная буква - 'm_Total'",
                "'m_Limit': открытое поле без префикса 'm_' - 'Limit'",
                "'m_Size': свойство без префикса 'm_' - 'Size'",
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
