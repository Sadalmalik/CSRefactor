using CSR.Core.Style;
using static CSR.Core.Tests.Arch.ArchTestCode;

namespace CSR.Core.Tests.Arch
{
    public class TypeChecksTests
    {
#region type-branch

        [TestCase("if (shape is Circle circle) return;", "Circle")]
        [TestCase("if (shape is Circle { Radius: 1 }) return;", "Circle")]
        [TestCase("if (shape is Circle) return;", "Circle")]
        [TestCase("var box = o as Box;", "Box")]
        [TestCase("if (o.GetType() == typeof(Box)) return;", "Box")]
        [TestCase("if (typeof(Box) != o.GetType()) return;", "Box")]
        public void TypeBranch_TypeCheck_Found(string body, string type)
        {
            var finding = Run("type-branch", InMethod(body)).Single();
            Assert.That(finding.Message, Does.Contain($"'{type}'"));
            Assert.That(finding.Line, Is.EqualTo(5));
        }

        [Test]
        public void TypeBranch_SwitchByTypes_OneFinding()
        {
            const string body = """
                switch (shape)
                {
                    case Circle circle:
                        break;
                    case Box box:
                        break;
                }
                var area = shape switch { Circle c => 1, Box b => 2, _ => 0 };
                """;
            var findings = Run("type-branch", InMethod(body));
            Assert.That(findings, Has.Count.EqualTo(2));
            Assert.That(findings[0].Message, Is.EqualTo("switch по типам: Circle, Box"));
            Assert.That(findings[1].Line, Is.EqualTo(12));
        }

        [Test]
        public void TypeBranch_ThisCheck_AsksAboutDerived()
        {
            var finding = Run("type-branch", InMethod("if (this is Derived) return;")).Single();
            Assert.That(finding.Message, Does.Contain("базовый класс знает наследника"));
        }

        [TestCase("if (o is IDisposable disposable) return;")]
        [TestCase("if (o is string text) return;")]
        [TestCase("if (o is int? number) return;")]
        [TestCase("if (o is null || o is not null || o is { }) return;")]
        [TestCase("if (o is State.Idle) return;")]
        [TestCase("var list = items.OfType<Box>();")]
        public void TypeBranch_NotTypeBranch_NoFinding(string body)
            => Assert.That(Run("type-branch", InMethod(body)), Is.Empty);

        [Test]
        public void TypeBranch_TypeParameterAndEquals_NoFinding()
        {
            const string source = """
                public class Sample<TItem>
                {
                    public T Get<T>(object o) where T : class
                        => o as T;

                    public bool Has(object o)
                        => o is TItem;

                    public override bool Equals(object other)
                        => other is Sample<TItem> sample;
                }
                """;
            Assert.That(Run("type-branch", source), Is.Empty);
        }

        [Test]
        public void TypeBranch_Options_IgnoreTypesAsCastInterfaces()
        {
            const string body = """
                var box = o as Box;
                if (o is Circle circle) return;
                if (o is IShape shape) return;
                """;
            var findings = Run("type-branch", InMethod(body), settings =>
            {
                settings.Set("asCast", false);
                settings.Set("ignoreTypes", new[] { "Circ*" });
                settings.Set("ignoreInterfaces", false);
            });
            Assert.That(findings.Single().Message, Does.Contain("'IShape'"));
        }

        [Test]
        public void TypeBranch_Paths_OnlyInSharedFolder()
        {
            var source = InMethod("if (shape is Circle circle) return;");
            Action<CheckSettings> configure = settings => settings.Set("paths", new[] { "*/Common/*" });
            Assert.That(Run("type-branch", source, configure, "Game/Common/Sample.cs"), Has.Count.EqualTo(1));
            Assert.That(Run("type-branch", source, configure, "Game/Ladder/Sample.cs"), Is.Empty);
        }

#endregion

#region behaviour-logic

        private const string Behaviour = """
            using UnityEngine;

            public class Marker : MonoBehaviour
            {
                private void Awake() { }

                private void OnDrawGizmosSelected() { }

                public Settings CreateSettings() => new Settings();

                public void Tick(float delta) { }

                [ContextMenu("Bake")]
                private void Bake() { }
            }
            """;

        [Test]
        public void BehaviourLogic_OwnMethods_Found()
        {
            var finding = Run("behaviour-logic", Behaviour).Single();
            Assert.That(finding.Line, Is.EqualTo(3));
            const string message = "MonoBehaviour 'Marker': своих методов 2 (порог 0): CreateSettings, Tick";
            Assert.That(finding.Message, Is.EqualTo(message));
        }

        [Test]
        public void BehaviourLogic_UnderThreshold_NoFinding()
            => Assert.That(Run("behaviour-logic", Behaviour, settings => settings.Set("maxMethods", 2)), Is.Empty);

        [Test]
        public void BehaviourLogic_ProjectBase_Found()
        {
            const string source = """
                public class Ladder : SceneMarker
                {
                    public float CalculateLength() => 0;
                }
                """;
            Assert.That(Run("behaviour-logic", source), Is.Empty);
            var findings = Run("behaviour-logic", source,
                settings => settings.Set("baseTypes", new[] { "SceneMarker" }));
            Assert.That(findings.Single().Message, Does.StartWith("SceneMarker 'Ladder'"));
        }

        [Test]
        public void BehaviourLogic_LookupInLogic_Found()
        {
            const string source = """
                public class Process
                {
                    public void Start(Component view)
                    {
                        var body = view.GetComponent<Rigidbody>();
                        var self = GetComponent<Rigidbody>();
                        var other = this.GetComponent<Rigidbody>();
                    }
                }

                public partial class Part
                {
                    public void Run(Component view) => view.TryGetComponent(out Rigidbody body);
                }
                """;
            var finding = Run("behaviour-logic", source).Single();
            Assert.That(finding.Line, Is.EqualTo(5));
            Assert.That(finding.Message, Does.Contain("'GetComponent' вне компонента сцены"));
        }

        [Test]
        public void BehaviourLogic_LookupInBehaviour_NoFinding()
        {
            const string source = """
                public class View : MonoBehaviour
                {
                    private void Awake() => m_Body = other.GetComponent<Rigidbody>();
                }
                """;
            Assert.That(Run("behaviour-logic", source), Is.Empty);
        }

#endregion
    }
}
