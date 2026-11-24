using CSR.Core.Graph;
using CSR.Core.Structure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Tests.Structure
{
    public class StructureTests
    {
        // Четыре уровня вложенных полиморфных частей: Root -> A | A1 -> B | B1 -> C | C1 -> D
        private const string Nested = """
            using System.Collections.Generic;

            public abstract class A { }
            public class A1 : A { private List<B> m_Parts; }
            public class A2 : A { }

            public abstract class B { }
            public class B1 : B { private List<C> m_Parts; }
            public class B2 : B { }

            public abstract class C { }
            public class C1 : C { private D[] m_Parts; }
            public class C2 : C { }

            public abstract class D { }
            public class D1 : D { }
            public class D2 : D { }

            public class Root { private List<A> m_Parts; }
            """;

        [Test]
        public void Ownership_DepthFourMarked_ThreeNot()
        {
            var result = Analyze(new StructureOptions(), In("Nested.cs", Nested));
            var root = result.Ownership.Single(chain => chain.Root.Name == "Root");
            var a1 = result.Ownership.Single(chain => chain.Root.Name == "A1");
            Assert.That(root.Depth, Is.EqualTo(4));
            Assert.That(
                root.Steps.Select(step => $"{step.Host.Name}->{step.ExtensionPoint.Name}"),
                Is.EqualTo(new[] { "Root->A", "A1->B", "B1->C", "C1->D" }));
            Assert.That(a1.Depth, Is.EqualTo(3));
            Assert.That(result.Ownership.Count(chain => chain.Depth > new StructureOptions().OwnershipDepth), Is.EqualTo(1));
        }

        [Test]
        public void Ownership_RecursionIsOneLevel()
        {
            const string source = """
                using System.Collections.Generic;
                public abstract class Expression { }
                public class Sum : Expression { private List<Expression> m_Items; }
                public class Constant : Expression { }
                public class Formula { private Expression m_Root; }
                """;
            var result = Analyze(new StructureOptions(), In("Formula.cs", source));
            var chain = result.Ownership.Single(item => item.Root.Name == "Formula");
            Assert.That(chain.Depth, Is.EqualTo(1));
        }

        [Test]
        public void FolderCycle_WithClosingLinks()
        {
            var result = Analyze(
                new StructureOptions(),
                In("Core/Engine.cs", "public class Engine { private Wheel m_Wheel; private Body m_Body; }"),
                In("Core/Body.cs", "public class Body { }"),
                In("Parts/Wheel.cs", "public class Wheel { private Engine m_Owner; }"),
                In("Parts/Bolt.cs", "public class Bolt { }"));
            var cycle = result.FolderCycles.Single();
            Assert.That(cycle.Packages, Is.EqualTo(new[] { "Core", "Parts" }));
            Assert.That(
                cycle.Links.Select(link => $"{link.From}->{link.To} {link.Count}/{link.Reverse}"),
                Is.EquivalentTo(new[] { "Core->Parts 1/1", "Parts->Core 1/1" }));
            Assert.That(cycle.Links[0].Example.Kind, Is.EqualTo(EdgeKind.Field));
        }

        [Test]
        public void GodObject_ThreeSigns()
        {
            const string source = """
                public class Stats { public int Health; public int Armor; }
                public class Weapon { public int Damage; public float Range; }
                public class Unit
                {
                    private int m_Level;
                    private int m_Experience;
                    private string m_Name;

                    public int Fight(Stats stats, Weapon weapon)
                    {
                        if (stats.Health > 0 && weapon.Damage > stats.Armor)
                            return weapon.Damage - stats.Armor;
                        return weapon.Range > 1 ? 1 : 0;
                    }

                    public void Grow(int points)
                    {
                        if (points > 0)
                            m_Experience += points;
                        if (m_Experience > 100)
                            m_Level++;
                    }

                    public string Describe()
                        => m_Name.Length > 0 ? m_Name : "unit";
                }
                """;
            var options = new StructureOptions { GodWmc = 5, GodAtfd = 3, GodTcc = 0.5 };
            var result = Analyze(options, In("Unit.cs", source));
            var unit = result.Types.Single(type => type.Node.Name == "Unit");
            Assert.That(unit.Facts!.Atfd, Is.EqualTo(4));
            Assert.That(unit.Facts.Tcc, Is.LessThan(0.5));
            Assert.That(unit.Facts.Wmc, Is.GreaterThanOrEqualTo(5));
            Assert.That(result.GetGods(options).Select(type => type.Node.Name), Is.EqualTo(new[] { "Unit" }));
            Assert.That(result.GetGods(new StructureOptions()), Is.Empty);
        }

        [Test]
        public void Package_InstabilityAndSiblings()
        {
            var result = Analyze(
                new StructureOptions(),
                In("Checks/Base.cs", "public abstract class Check { }"),
                In("Checks/All/First.cs", "public class First : Check { }"),
                In("Checks/All/Second.cs", "public class Second : Check { }"),
                In("Checks/All/Third.cs", "public class Third : Check { }"));
            var all = result.Folders.Single(package => package.Name == "Checks/All");
            Assert.That(all.Efferent, Is.EqualTo(1));
            Assert.That(all.Instability, Is.EqualTo(1));
            Assert.That(all.Communities, Is.EqualTo(1));
            Assert.That(all.IsolatedShare, Is.EqualTo(0));
            var checks = result.Folders.Single(package => package.Name == "Checks");
            Assert.That(checks.Abstractness, Is.EqualTo(1));
            Assert.That(checks.Distance, Is.EqualTo(0));
        }

        private static StructureResult Analyze(StructureOptions options, params SourceFile[] files)
        {
            var trees = files.Select(file => CSharpSyntaxTree.ParseText(file.Text, path: file.Path)).ToList();
            var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
            var compilation = CSharpCompilation.Create("test", trees, references);
            var graph = TypeGraphBuilder.Build([compilation], trees, path => path, CancellationToken.None);
            return StructureAnalysis.Run(graph, [compilation], trees, options, CancellationToken.None);
        }

        private static SourceFile In(string path, string text)
            => new(path, text);

        private class SourceFile
        {
            public SourceFile(string path, string text)
            {
                Path = path;
                Text = text;
            }

            public string Path { get; }

            public string Text { get; }
        }
    }
}
