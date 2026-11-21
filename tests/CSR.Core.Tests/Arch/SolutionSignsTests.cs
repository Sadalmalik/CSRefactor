using static CSR.Core.Tests.Arch.SolutionChecksTests;

namespace CSR.Core.Tests.Arch
{
    public class SolutionSignsTests
    {
#region single-implementation

        [Test]
        public void SingleImplementation_OneClass_Found()
        {
            const string source = """
                public interface IScorer
                {
                    float Score();
                }

                public class Scorer : IScorer
                {
                    public float Score() => 0;
                }
                """;
            var finding = Analyze([Compile("Game", "Scorer.cs", source)], "single-implementation").Single();
            Assert.That(finding.Line, Is.EqualTo(1));
            Assert.That(finding.Message, Is.EqualTo("интерфейс 'IScorer' - одна реализация: 'Scorer' (Scorer.cs)"));
        }

        [Test]
        public void SingleImplementation_TwoOrContractOrMarker_NoFinding()
        {
            const string source = """
                public interface IPathAgent
                {
                    void Move();
                }

                public class UnitAgent : IPathAgent { public void Move() { } }

                public abstract class AgentBase : IPathAgent { public abstract void Move(); }

                public class TestAgent : AgentBase { public override void Move() { } }

                // Реализуют построители маршрута: якорные и плющ
                public interface IBuilder
                {
                    void Build();
                }

                public class Builder : IBuilder { public void Build() { } }

                public interface IMarker
                {
                }

                public class Marked : IMarker { }
                """;
            Assert.That(Analyze([Compile("Game", "Agents.cs", source)], "single-implementation"), Is.Empty);
        }

#endregion

#region dead-member

        private const string Process = """
            public class Process
            {
                private int m_Unused;
                private float m_Written;
                private bool m_Read;

                internal int Count { get; set; }

                public void Tick(float delta, int index)
                {
                    m_Written = delta;
                    if (m_Read)
                        Helper(delta);
                }

                private void Helper(float value) { }

                private void Orphan() { }

                public override string ToString() => "";

                private void Update() { }
            }

            public class User
            {
                public void Run(Process process) => process.Tick(1, 2);
            }
            """;

        [Test]
        public void DeadMember_UnusedWriteOnlyAndParameters_Found()
        {
            var findings = Analyze([Compile("Game", "Process.cs", Process)], "dead-member");
            var messages = findings.Select(finding => finding.Message).ToList();
            Assert.That(messages, Is.EqualTo(new[]
            {
                "поле 'Process.m_Unused' не используется",
                "поле 'Process.m_Written' только записывается: мест 1",
                "свойство 'Process.Count' не используется",
                "параметр 'index' метода 'Process.Tick' не читается",
                "параметр 'value' метода 'Process.Helper' не читается",
                "метод 'Process.Orphan' не используется",
            }));
        }

        [Test]
        public void DeadMember_Options_PublicAndWithoutParameters()
        {
            var compilation = Compile("Game", "Process.cs", Process);
            var findings = Analyze([compilation], "dead-member", settings =>
            {
                settings.Set("accessibility", new[] { "public" });
                settings.Set("parameters", false);
            });
            Assert.That(findings.Select(finding => finding.Message),
                Is.EqualTo(new[] { "метод 'User.Run' не используется" }));
        }

        [Test]
        public void DeadMember_BoundAndSerialized_NoFinding()
        {
            const string source = """
                public interface IHandler { void Handle(int value); }

                public class Handler : IHandler
                {
                    [UnityEngine.SerializeField]
                    private int m_Speed;

                    public void Handle(int value) { }

                    private void OnEnable() { }

                    protected virtual void OnHook(int value) { }

                    private void Subscribe(System.Action<int> callback) => callback(1);

                    private void Callback(int value) { }

                    public void Run() => Subscribe(Callback);
                }
                """;
            var stubs = "namespace UnityEngine { public class SerializeFieldAttribute : System.Attribute { } }";
            var findings = Analyze([Compile("Game", "Handler.cs", source, "Stubs.cs", stubs)], "dead-member",
                settings => settings.Set("accessibility", new[] { "private", "internal", "protected" }));
            Assert.That(findings, Is.Empty);
        }

#endregion

#region parameter-set

        [Test]
        public void ParameterSet_ForwardedSet_Found()
        {
            const string source = """
                public class Mover
                {
                    public void Move(int speed, int angle, int height, int depth, out int result) => result = 0;

                    public void Step(int speed, int angle, int height, int depth)
                    {
                        Move(speed, angle, height, depth, out int result);
                    }

                    public void Jump() => Move(1, 2, 3, 4, out int result);

                    public void Fall() => Move(1, 2, 3, 4, out int result);

                    public void Short(int a, int b, int c) { }

                    public void UseShort() { Short(1, 2, 3); Short(1, 2, 3); Short(1, 2, 3); }
                }
                """;
            var finding = Analyze([Compile("Game", "Mover.cs", source)], "parameter-set").Single();
            const string message = "метод 'Mover.Move' (4 входных параметров): вызовов 3, "
                + "из них 1 передают параметры вызывающего дальше";
            Assert.That(finding.Message, Is.EqualTo(message));

            var strict = Analyze([Compile("Game", "Mover.cs", source)], "parameter-set",
                settings => settings.Set("minForwarded", 2));
            Assert.That(strict, Is.Empty);
        }

#endregion

#region persisted-class

        [Test]
        public void PersistedClass_ConstructorsDerivedAndCache_Found()
        {
            const string source = """
                public class SaveState
                {
                    [JsonProperty]
                    private int m_Health;

                    private float m_CachedSpeed;

                    [JsonIgnore]
                    private float m_SpeedCache;

                    public SaveState(int health)
                    {
                        m_Health = health;
                    }

                    [JsonProperty]
                    public int Double => m_Health * 2;
                }
                """;
            var findings = ArchTestCode.Run("persisted-class", source);
            Assert.That(findings.Select(finding => finding.Message), Is.EqualTo(new[]
            {
                "сохраняемый 'SaveState' без пустого конструктора десериализации",
                "поле-кэш 'm_CachedSpeed' без явного исключения из сохранения",
                "производное свойство 'Double' сохраняется",
            }));
        }

        [Test]
        public void PersistedClass_PairOfConstructorsOrNotPersisted_NoFinding()
        {
            const string source = """
                public class SaveState
                {
                    [JsonProperty]
                    private int m_Health;

                    public SaveState() { }

                    public SaveState(int health) { m_Health = health; }
                }

                public class Runtime
                {
                    private float m_Cache;

                    public Runtime(int value) { }
                }
                """;
            Assert.That(ArchTestCode.Run("persisted-class", source), Is.Empty);
        }

#endregion
    }
}
