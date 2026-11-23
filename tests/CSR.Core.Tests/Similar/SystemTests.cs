using CSR.Core.Graph;
using CSR.Core.Similar;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Tests.Similar
{
    public class SystemTests
    {
        private const string Library = """
            public struct Vector3 { public float X; public float Y; public float Z; }
            public struct Quaternion { public float X; public float W; }
            """;

        // Движения: хост держит список абстрактных обработчиков, обработчик отдает кадр движения
        private const string Motions = """
            using System.Collections.Generic;

            public struct MotionFrame { public Vector3 Move; public Vector3 Look; public bool IsGrounded; }

            public abstract class MotionHandler
            {
                public abstract MotionFrame Calculate(float deltaTime);
            }

            public class WalkHandler : MotionHandler
            {
                public override MotionFrame Calculate(float deltaTime) => new MotionFrame { IsGrounded = true };
            }

            public class JumpHandler : MotionHandler
            {
                public override MotionFrame Calculate(float deltaTime) => new MotionFrame();
            }

            public class MotionGroup
            {
                private readonly List<MotionHandler> m_Handlers = new();

                public MotionFrame Tick(float deltaTime)
                {
                    var frame = new MotionFrame();
                    foreach (var handler in m_Handlers)
                        frame = handler.Calculate(deltaTime);
                    return frame;
                }
            }
            """;

        // Моторы снарядов: другие имена и тела, то же устройство
        private const string Motors = """
            using System.Collections.Generic;

            public struct MotorResult { public Vector3 Position; public Quaternion Rotation; }

            public abstract class ProjectileMotor
            {
                public abstract MotorResult Step(float time);
            }

            public class ArrowMotor : ProjectileMotor
            {
                public override MotorResult Step(float time) => new MotorResult();
            }

            public class OrbitMotor : ProjectileMotor
            {
                public override MotorResult Step(float time) { var result = new MotorResult(); return result; }
            }

            public class MissileMotor : ProjectileMotor
            {
                public override MotorResult Step(float time) => default;
            }

            public class ProjectileMotors
            {
                private ProjectileMotor[] m_Motors;

                public void Update(float time)
                {
                    foreach (var motor in m_Motors)
                        Apply(motor.Step(time));
                }

                private void Apply(MotorResult result) { }
            }
            """;

        [Test]
        public void Graph_Edges()
        {
            var graph = Build(Library, Motions);
            var group = graph.Nodes.Single(node => node.Name == "MotionGroup");
            var handler = graph.Nodes.Single(node => node.Name == "MotionHandler");
            Assert.That(Describe(group), Is.EquivalentTo(new[]
            {
                "Collection MotionHandler",
                "Returns MotionFrame",
                "Creates MotionFrame",
                "CallsVirtual MotionHandler",
            }));
            Assert.That(handler.IsExtensionPoint, Is.True);
            Assert.That(
                handler.GetIn(EdgeKind.Inherits).Select(edge => edge.From.Name),
                Is.EquivalentTo(new[] { "WalkHandler", "JumpHandler" }));
            Assert.That(handler.ExtensionSignatures, Is.EqualTo(new[] { "метод структура(float)" }));
            Assert.That(
                graph.Nodes.Single(node => node.Name == "MotionFrame").FieldTypes,
                Is.EqualTo(new[] { "структура", "структура", "bool" }));
        }

        [Test]
        public void ExtensionHosts_Inventory()
        {
            var entry = ExtensionHosts.Find(Build(Library, Motors), 2).Single();
            Assert.That(entry.Host.Name, Is.EqualTo("ProjectileMotors"));
            Assert.That(entry.ExtensionPoint.Name, Is.EqualTo("ProjectileMotor"));
            Assert.That(entry.IsCollection, Is.True);
            Assert.That(
                entry.Variants.Select(node => node.Name),
                Is.EqualTo(new[] { "ArrowMotor", "MissileMotor", "OrbitMotor" }));
            Assert.That(entry.Results.Single().Name, Is.EqualTo("MotorResult"));
        }

        [Test]
        public void TwoSystemsOfOneDesign_Cluster()
        {
            var options = new SimilarOptions { Kinds = [UnitKind.System], MinSystemTypes = 3 };
            var units = SystemUnits.Collect(Build(Library, Motions, Motors), options);
            Assert.That(units.Select(unit => unit.Member), Is.EquivalentTo(new[] { "MotionGroup", "ProjectileMotors" }));

            var result = SimilarSearch.Run(units, options);
            var pair = result.Pairs.Single();
            Assert.That(pair.Score, Is.GreaterThanOrEqualTo(options.SystemThreshold));
            var cluster = result.Clusters.Single();
            Assert.That(cluster.Common, Does.Contain("база: возврат структура").And.Contain("база: параметр float"));
            Assert.That(cluster.Bodies.Values.SelectMany(lines => lines), Does.Contain("результат: MotorResult"));
        }

        [Test]
        public void HostWithConcreteField_NotSystem()
        {
            const string source = """
                public class Engine { public int Power; }
                public class Car
                {
                    private Engine m_Engine;
                    private Engine[] m_Spares;
                }
                """;
            Assert.That(ExtensionHosts.Find(Build(source), 2), Is.Empty);
        }

        [Test]
        public void VariantsOfOneSystem_NotPair()
        {
            var options = new SimilarOptions { Kinds = [UnitKind.System], MinSystemTypes = 3 };
            const string host = """
                using System.Collections.Generic;
                public class MotionLayer
                {
                    private List<MotionHandler> m_Handlers;
                    public MotionFrame Tick(float time) => default;
                }
                """;
            var units = SystemUnits.Collect(Build(Library, Motions, host), options);
            Assert.That(units, Has.Count.EqualTo(2));
            Assert.That(SimilarSearch.Run(units, options).Pairs, Is.Empty);
        }

        private static TypeGraph Build(params string[] sources)
        {
            var trees = sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, path: $"File{index}.cs")).ToList();
            var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
            var compilation = CSharpCompilation.Create("test", trees, references);
            return TypeGraphBuilder.Build([compilation], trees, path => path, CancellationToken.None);
        }

        private static IEnumerable<string> Describe(TypeNode node)
            => node.Out.Select(edge => $"{edge.Kind} {edge.To.Name}");
    }
}
