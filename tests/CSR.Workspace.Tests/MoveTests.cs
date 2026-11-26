using CSR.Workspace.Loading;
using CSR.Workspace.Refactor;
using CSR.Workspace.Symbols;
using Microsoft.CodeAnalysis;

namespace CSR.Workspace.Tests
{
    // csr move: перенос типа в свой файл. План - на своей копии решения с добавленными файлами, запись - на отдельной
    public class MoveTests
    {
        private const string Tools = """
            using System.Text;
            using Sample.Units;

            namespace Sample.Items
            {
                public static class Labels
                {
                    public static string Join(string a, string b)
                        => new StringBuilder(a).Append(b).ToString();

                    public class Nested
                    {
                    }
                }

                public class Marker
                {
                    public Player? Owner;
                }
            }

            """;

        private const string Wallet = """
            namespace Sample.Items
            {
                public class Wallet
                {
                    public int Gold;
                }
            }

            """;

        private const string Meta = "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n";

        private SampleSolution m_Sample = null!;
        private LoadedSolution m_Solution = null!;

        [OneTimeSetUp]
        public async Task Load()
        {
            m_Sample = CreateSample();
            m_Solution = await m_Sample.LoadAsync();
        }

        [OneTimeTearDown]
        public void Unload()
        {
            m_Solution.Dispose();
            m_Sample.Dispose();
        }

        [Test]
        public async Task Move_TypeToOwnFile()
        {
            var plan = await PlanAsync(m_Solution, "Sample.Items.Item");

            Assert.That(plan.Refusal, Is.Null);
            Assert.That(plan.Target, Is.EqualTo("Game/Item.cs"));
            var kinds = plan.Changes.Select(change => (change.File, change.Kind));
            Assert.That(kinds, Is.EqualTo(new[]
            {
                ("Game/Inventory.cs", FileChangeKind.Modified),
                ("Game/Item.cs", FileChangeKind.Added),
            }));
            var added = plan.Changes[1].After.ToString().Replace("\r\n", "\n");
            Assert.That(
                added,
                Does.StartWith("namespace Sample.Items\n{\n    [Serializable]\n    public class Item\n"));
            Assert.That(added, Does.EndWith("        public int Width, Height;\n    }\n}\n"));
            var source = plan.Changes[0].After.ToString().Replace("\r\n", "\n");
            Assert.That(source, Does.StartWith("namespace Sample.Items\n{\n    public class Inventory\n"));
            Assert.That(plan.Diff, Does.Not.Contain("+    public class Inventory"), "строки без правок - не в диффе");
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
        }

        [Test]
        public async Task Move_UsingsFollowType()
        {
            var plan = await PlanAsync(m_Solution, "Sample.Items.Labels");

            var added = plan.Changes.Single(change => change.Kind == FileChangeKind.Added).After.ToString();
            Assert.That(added.Replace("\r\n", "\n"), Does.StartWith("using System.Text;\n\nnamespace Sample.Items\n"));
            var source = plan.Changes.Single(change => change.Kind == FileChangeKind.Modified).After.ToString();
            Assert.That(
                source.Replace("\r\n", "\n"),
                Does.StartWith("using Sample.Units;\n\nnamespace Sample.Items\n{\n"));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
        }

        [Test]
        public async Task Move_Refusals()
        {
            Assert.That(
                (await PlanAsync(m_Solution, "Sample.Items.Labels.Nested")).Refusal,
                Does.StartWith("вложенный"));
            Assert.That((await PlanAsync(m_Solution, "Sample.Items.Inventory")).Refusal, Does.StartWith("тип уже"));
            var exists = await PlanAsync(m_Solution, "Sample.Items.Marker", "Game/Player.cs");
            Assert.That(exists.Refusal, Does.Contain("уже есть"));
            var outside = await PlanAsync(m_Solution, "Sample.Items.Marker", "../Marker.cs");
            Assert.That(outside.Refusal, Does.StartWith("файл назначения вне папки проекта"));
        }

        [Test]
        public async Task Move_OnlyTypeMovesFileWithMeta()
        {
            using var sample = CreateSample();
            using var loaded = await sample.LoadAsync();
            var plan = await PlanAsync(loaded, "Sample.Items.Wallet");

            Assert.That(plan.Changes.Select(change => change.Kind), Is.All.EqualTo(FileChangeKind.Renamed));
            Assert.That(plan.Diff, Does.Contain("rename from Game/Misc.cs\nrename to Game/Wallet.cs\n"));
            Assert.That(plan.Diff, Does.Contain("rename from Game/Misc.cs.meta\nrename to Game/Wallet.cs.meta\n"));

            FileChangeTools.Apply(plan.Changes);
            Assert.That(File.Exists(sample.GetPath("Game/Misc.cs")), Is.False);
            Assert.That(File.ReadAllText(sample.GetPath("Game/Wallet.cs")), Is.EqualTo(Wallet));
            Assert.That(File.ReadAllText(sample.GetPath("Game/Wallet.cs.meta")), Is.EqualTo(Meta), "GUID сохранен");
        }

        private static SampleSolution CreateSample()
        {
            var sample = SampleSolution.Create();
            File.WriteAllText(sample.GetPath("Game/Tools.cs"), Tools);
            File.WriteAllText(sample.GetPath("Game/Misc.cs"), Wallet);
            File.WriteAllText(sample.GetPath("Game/Misc.cs.meta"), Meta);
            return sample;
        }

        private static async Task<RefactorPlan> PlanAsync(LoadedSolution loaded, string symbol, string? target = null)
        {
            var query = SymbolQuery.Parse(symbol);
            var symbols = await SymbolResolver.ResolveAsync(loaded, query, loaded.Root, false);
            var type = (INamedTypeSymbol)symbols[0];
            return await TypeMover.PlanAsync(loaded, type, target, loaded.Root, true);
        }
    }
}
