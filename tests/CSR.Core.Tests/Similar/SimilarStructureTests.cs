using CSR.Core.Similar;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Tests.Similar
{
    public class SimilarStructureTests
    {
#region L2

        private const string Projectiles = """
            public class Projectiles
            {
                public void Launch(Vector3 position, Vector3 direction, float speed, int damage)
                {
                }
            }
            """;

        private const string Units = """
            public class Units
            {
                public void Move(Vector3 pos, Vector3 dir, float speed, string animation, out bool isMoved)
                {
                    isMoved = true;
                }
            }
            """;

        [Test]
        public void ParameterSets_Synonyms_FormPair()
        {
            var options = Only(UnitKind.ParameterSet);
            Assert.That(Run(options, Projectiles, Units).Pairs, Is.Empty);

            options.Synonyms["pos"] = "position";
            options.Synonyms["dir"] = "direction";
            var result = Run(options, Projectiles, Units);
            var pair = result.Pairs.Single();
            Assert.That(pair.Score, Is.EqualTo(0.6));
            var cluster = result.Clusters.Single();
            Assert.That(cluster.Common, Is.EqualTo(new[] { "Vector3 position", "Vector3 direction", "float speed" }));
            Assert.That(
                cluster.Differences.Values.Select(own => own.Single()),
                Is.EquivalentTo(new[] { "int damage", "string animation" }));
        }

        [Test]
        public void ParameterSets_OverrideSkipped()
        {
            const string derived = """
                public class Derived : Projectiles
                {
                    public override void Launch(Vector3 position, Vector3 direction, float speed, int damage)
                    {
                    }
                }
                """;
            Assert.That(Run(Only(UnitKind.ParameterSet), Projectiles, derived).Pairs, Is.Empty);
        }

        [Test]
        public void FieldSets_PrefixStripped_AutoPropertiesCount()
        {
            const string state = """
                public class MotorState
                {
                    private Vector3 m_Position;
                    private Vector3 m_Velocity;
                    private float m_Mass;
                    private static int s_Count;
                    private const int Limit = 3;
                }
                """;
            const string data = """
                public class BodyData
                {
                    public Vector3 Position { get; set; }
                    public Vector3 Velocity { get; set; }
                    public float Mass { get; init; }
                    public string Name { get; set; }
                }
                """;
            var pair = Run(Only(UnitKind.FieldSet), state, data).Pairs.Single();
            Assert.That(pair.Score, Is.EqualTo(0.75));
            Assert.That(
                pair.Right.Elements,
                Is.EqualTo(new[] { "Vector3 position", "Vector3 velocity", "float mass", "string name" }));
        }

#endregion


#region L3

        private const string FirstRule = """
            public class FirstRule : RuleBase
            {
                private static readonly string[] Masks = ["a*", "b*"];

                public override string Name
                    => "first";

                public override void Apply(Context context)
                {
                    foreach (var node in context.Nodes)
                    {
                        if (Matches(node.Text) && node.Depth > 2)
                            context.Report(node, "first: " + node.Text);
                    }
                }

                private static bool Matches(string text)
                    => Masks.Any(mask => text.StartsWith(mask.TrimEnd('*')));
            }
            """;

        [Test]
        public void SameSchemaDifferentData_ScoreOne()
        {
            var second = FirstRule.Replace("FirstRule", "SecondRule").Replace("\"first", "\"second").Replace("\"a*\"", "\"x*\"")
                .Replace("> 2", "> 5");
            var result = Run(Only(UnitKind.Type), FirstRule, second);
            var pair = result.Pairs.Single();
            Assert.That(pair.Score, Is.EqualTo(1.0));
            var bodies = result.Clusters.Single().Bodies.Values.First();
            Assert.That(bodies, Is.EqualTo(new[] { "та же форма (отличия - имена и литералы): членов 4 из 4" }));
        }

        [Test]
        public void DifferentClassesWithSameBase_BelowThreshold()
        {
            const string other = """
                public class OtherRule : RuleBase
                {
                    private readonly Dictionary<string, int> m_Counts = new();

                    public override string Name
                        => "other";

                    public override void Apply(Context context)
                    {
                        var groups = context.Nodes.GroupBy(node => node.Kind).ToList();
                        while (groups.Count > 0)
                        {
                            m_Counts[groups[0].Key] = groups[0].Count();
                            groups.RemoveAt(0);
                        }

                        context.Flush(m_Counts.Values.Sum());
                    }

                    public int GetCount(string kind)
                        => m_Counts.TryGetValue(kind, out int count) ? count : 0;
                }
                """;
            Assert.That(Run(Only(UnitKind.Type), FirstRule, other).Pairs, Is.Empty);
        }

        [Test]
        public void SharedCore_PairBelowThreshold()
        {
            const string core = """
                    public int LeftPoints => Mathf.RoundToInt(Fraction * MaxPoints);
                    public bool IsFull => Fraction >= 1f;
                    public float PointsToFraction(int points)
                    {
                        int maxPoints = MaxPoints;
                        return maxPoints > 0 ? (float)points / maxPoints : 0f;
                    }
                    public void OnHandle(in ChangedEvent evt)
                    {
                        LastHandled = default;
                        Notify(evt.Source, LastHandled);
                    }
                """;
            var breath = $$"""
                public class PartBreath : EntityPart
                {
                {{core}}
                    public void EnterWinded(float duration) { m_Winded = true; m_Timer = duration; Fx.Spawn(Owner, "winded"); }
                    public void Tick(float delta) { if (m_Winded && (m_Timer -= delta) <= 0) ExitWinded(); else Regen(delta * 2); }
                    public string Describe() => string.Join(",", m_Log.Select(entry => entry.ToString()));
                }
                """;
            var poise = $$"""
                public class PartPoise : EntityPart, INetworkObject
                {
                {{core}}
                    public bool IsBroken { get => m_Broken.Value; set => m_Broken.Value = value; }
                    public void Break(Entity source) { foreach (var item in source.Items) Drop(item); Controller.Break(this); }
                }
                """;
            var options = Only(UnitKind.Type);
            var pair = Run(options, breath, poise).Pairs.Single();
            Assert.That(pair.Score, Is.LessThan(options.TypeThreshold));
            Assert.That(pair.SharedMembers, Is.EqualTo(4));

            options.MinCoreMembers = 0;
            Assert.That(Run(options, breath, poise).Pairs, Is.Empty);
        }

        [Test]
        public void DataTypes_NotTypeUnits()
        {
            const string data = """
                public class Settings
                {
                    public const string First = "a";
                    public const string Second = "b";
                    public int Count;
                    public Settings(int count) { Count = count; }
                }
                """;
            var units = TypeUnits.Collect(CSharpSyntaxTree.ParseText(data, path: "Settings.cs"), new SimilarOptions());
            Assert.That(units, Is.Empty);
        }

#endregion

        private static SimilarOptions Only(UnitKind kind)
            => new() { Kinds = [kind] };

        private static SimilarResult Run(SimilarOptions options, params string[] sources)
        {
            var units = sources
                .Select((source, index) => CSharpSyntaxTree.ParseText(source, path: $"File{index}.cs"))
                .SelectMany(tree => SimilarUnits.Collect(tree, options))
                .ToList();
            return SimilarSearch.Run(units, options);
        }
    }
}
