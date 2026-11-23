using CSR.Core.Checks;
using CSR.Core.Config;
using CSR.Core.Style;
using CSR.Core.Style.Naming;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Tests.Style
{
    // Ядро naming: сбор символов, селектор, каскад по ключу требования, уровень и метка правила, реестр
    public class NamingCoreTests
    {
        private const string Source = """
            namespace Game.Units
            {
                [Serializable]
                public class Door : MonoBehaviour, IOpenable
                {
                    private const int Max = 3;
                    private static bool s_Any;
                    private bool m_IsOpen;
                    public event Action Opened;
                    public bool IsLocked { get; set; }
                    public override bool Equals(object other) => false;

                    private void Toggle<TValue>(bool force, List<int> ids)
                    {
                        var count = 1;
                        bool done = false;
                    }
                }

                public interface IOpenable
                {
                }
            }
            """;

        [Test]
        public void Symbols_KindsAccessModifiersTypes()
        {
            var symbols = Collect(Source);

            var names = symbols.Select(symbol => $"{symbol.Kind} {symbol.Name}");
            Assert.That(names, Is.EqualTo(new[]
            {
                "namespace Game.Units",
                "class Door",
                "field Max",
                "field s_Any",
                "field m_IsOpen",
                "event Opened",
                "property IsLocked",
                "typeParameter TValue",
                "parameter force",
                "parameter ids",
                "local count",
                "local done",
                "interface IOpenable",
            }));
            var door = symbols[1];
            Assert.That(door.Bases, Is.EqualTo(new[] { "MonoBehaviour", "IOpenable" }));
            Assert.That(door.Attributes, Is.EqualTo(new[] { "Serializable" }));
            Assert.That(door.Namespace, Is.EqualTo("Game.Units"));
            Assert.That(symbols[2].Label, Is.EqualTo("константа"));
            Assert.That(symbols[3].Label, Is.EqualTo("закрытое статическое поле"));
            Assert.That(symbols[3].Modifiers, Does.Contain("static"));
            Assert.That(symbols[4].TypeName, Is.EqualTo("bool"));
            Assert.That(symbols[5].TypeName, Is.EqualTo("Action"));
            Assert.That(symbols[9].TypeName, Is.EqualTo("List<int>"));
            Assert.That(symbols[10].TypeName, Is.Null);
        }

        // Остальные виды; имена, заданные базой, не собираются; атрибуты с целью - не атрибуты объявления
        [Test]
        public void Symbols_OtherKindsInheritedAndTargetedAttributes()
        {
            const string source = """
                struct Cell { }
                record Item(int Count);
                enum Mode : byte { A }
                delegate void Handler();

                class Base : Parent, IView
                {
                    public override bool IsReady { get; }
                    bool IView.IsShown => true;
                    public override event Action Changed;
                    [field: SerializeField]
                    public int Value { get; private set; }

                    void Read() => Parse("1", out bool parsed);
                }

                interface IView
                {
                    bool IsShown { get; }
                }
                """;

            var symbols = Collect(source);

            var names = symbols.Select(symbol => $"{symbol.Kind} {symbol.Name} {symbol.Access}");
            Assert.That(names, Is.EqualTo(new[]
            {
                "struct Cell internal",
                "record Item internal",
                "parameter Count ",
                "enum Mode internal",
                "delegate Handler internal",
                "class Base internal",
                "property Value public",
                "local parsed ",
                "interface IView internal",
                "property IsShown public",
            }));
            Assert.That(symbols.Single(symbol => symbol.Name == "Value").Attributes, Is.Empty);
        }

        [Test]
        public void Cascade_FirstRulePerKey_SeverityAndRuleOfRuleOrCheck()
        {
            var fields = Selector(new() { ["kinds"] = new[] { "field" } });
            var privateFields = Selector(new() { ["kinds"] = new[] { "field" }, ["access"] = new[] { "private" } });
            var bools = Selector(new() { ["types"] = new[] { "bool" } });
            var rules = new List<NamingRule>
            {
                new(privateFields, Requirements("prefix", "первое"), Severity.Hint, "R-prefix"),
                new(fields, Requirements("prefix", "второе")),
                new(bools, Requirements("words", "слова")),
            };

            var findings = Run(rules);

            var lines = findings.Select(finding => $"{finding.Line} {finding.Message} {finding.Severity} {finding.Rule}");
            Assert.That(lines, Is.EqualTo(new[]
            {
                "6 первое Hint R-prefix",
                "7 первое Hint R-prefix",
                "7 слова Error R-check",
                "8 первое Hint R-prefix",
                "8 слова Error R-check",
                "10 слова Error R-check",
                "13 слова Error R-check",
                "16 слова Error R-check",
            }));
        }

        [Test]
        public void Selector_ModifiersAttributesBases()
        {
            var rules = new List<NamingRule>
            {
                new(Selector(new() { ["modifiers"] = new[] { "static" } }), Requirements("a", "static")),
                new(Selector(new() { ["attributes"] = new[] { "Serial*" } }), Requirements("b", "атрибут")),
                new(Selector(new() { ["bases"] = new[] { "Mono*" } }), Requirements("c", "база")),
            };

            var messages = Run(rules).Select(finding => $"{finding.Line} {finding.Message}");

            Assert.That(messages, Is.EqualTo(new[] { "4 атрибут", "4 база", "7 static" }));
        }

        [TestCase("kinds", "feild", "kinds: 'feild' - допустимо")]
        [TestCase("access", "privat", "access: 'privat' - допустимо")]
        [TestCase("modifiers", "statc", "modifiers: 'statc' - допустимо")]
        public void Selector_UnknownValue_FormatError(string field, string value, string message)
        {
            var exception = Assert.Throws<FormatException>(() => Selector(new() { [field] = new[] { value } }));

            Assert.That(exception!.Message, Does.StartWith(message));
        }

        // Ключ каскада у вида требования - один на вид; поля видов не пересекаются между собой и с полями правила
        [Test]
        public void Catalog_KeysAndFieldsUnique()
        {
            var keys = NamingCatalog.Kinds.Select(kind => kind.Key).ToList();
            var fields = NamingCatalog.Kinds.SelectMany(kind => kind.Fields).Concat(NamingRule.OwnFields).ToList();

            Assert.That(keys, Is.Unique);
            Assert.That(fields, Is.Unique);
        }

        private static List<NameSymbol> Collect(string source)
            => NameSymbols.Collect(CSharpSyntaxTree.ParseText(source).GetRoot()).ToList();

        private static List<Finding> Run(IReadOnlyList<NamingRule> rules)
        {
            var configuration = CheckConfiguration.Create(StyleCatalog.Find("naming")!, Severity.Error, "R-check");
            var project = new ProjectOptions();
            var tree = CSharpSyntaxTree.ParseText(Source, project.ParseOptions, "Door.cs");
            var findings = new List<Finding>();
            NamingCheck.Run(new CheckContext(tree, project, configuration, findings), rules);
            return findings.OrderBy(finding => finding.Line).ToList();
        }

        private static NameSelector Selector(Dictionary<string, object> fields)
            => NameSelector.Parse(new Record(fields));

        // Одно тестовое требование под ключом key
        private static Dictionary<string, INameRequirement> Requirements(string key, string message)
            => new() { [key] = new Fail(message) };

        // Тестовое требование: любое имя - нарушение с заданным текстом
        private class Fail : INameRequirement
        {
            private readonly string m_Message;

            public Fail(string message)
            {
                m_Message = message;
            }

            public string? Check(NameSymbol symbol)
                => m_Message;
        }
    }
}
