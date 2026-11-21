using CSR.Workspace.Loading;
using CSR.Workspace.Refactor;
using CSR.Workspace.Rename;
using CSR.Workspace.Symbols;
using Microsoft.CodeAnalysis;

namespace CSR.Workspace.Tests
{
    // csr move члена в другой тип на своей копии решения с файлом Game/Shop.cs
    public class MoveMemberTests
    {
        private const string Shop = """
            namespace Sample.Items
            {
                public static class Pricing
                {
                    public const int Base = 10;

                    public static int Price(Item item)
                        => Base + item.Count * Rate();

                    public static int Rate()
                        => 2;
                }

                public class Shop
                {
                    private int m_Sold;

                    public int Sell(Item item)
                    {
                        m_Sold++;
                        return Pricing.Price(item);
                    }

                    public void Label(Item item, string title)
                    {
                        item.Title = title;
                        Console.WriteLine(item.Title);
                    }

                    public void Use(Item item)
                        => Label(item, "x");

                    public int Total()
                        => m_Sold;
                }
            }

            """;

        private static readonly RefactorOptions Options = new() { SerializationAttributes = ["Sample.Storage.Saved"] };

        private SampleSolution m_Sample = null!;
        private LoadedSolution m_Solution = null!;

        [OneTimeSetUp]
        public async Task Load()
        {
            m_Sample = SampleSolution.Create();
            File.WriteAllText(m_Sample.GetPath("Game/Shop.cs"), Shop);
            m_Solution = await m_Sample.LoadAsync();
        }

        [OneTimeTearDown]
        public void Unload()
        {
            m_Solution.Dispose();
            m_Sample.Dispose();
        }

        [Test]
        public async Task Static_CallersQualified()
        {
            var plan = await PlanAsync("Sample.Items.Pricing.Rate", "Sample.Items.Inventory");

            Assert.That(plan.Refusal, Is.Null);
            var shop = GetText(plan, "Game/Shop.cs");
            Assert.That(shop, Does.Contain("=> Base + item.Count * Inventory.Rate();"));
            Assert.That(shop, Does.Not.Contain("public static int Rate()"));
            var inventory = GetText(plan, "Game/Inventory.cs");
            Assert.That(
                inventory,
                Does.Contain("        }\n\n        public static int Rate()\n            => 2;\n    }\n}"));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
        }

        [Test]
        public async Task Static_BodyQualifiesSourceMembers()
        {
            var plan = await PlanAsync("Sample.Items.Pricing.Price", "Sample.Items.Inventory");

            var inventory = GetText(plan, "Game/Inventory.cs");
            Assert.That(inventory, Does.Contain("=> Pricing.Base + item.Count * Pricing.Rate();"));
            Assert.That(GetText(plan, "Game/Shop.cs"), Does.Contain("return Inventory.Price(item);"));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
        }

        [Test]
        public async Task Instance_ReceiverParameterBecomesThis()
        {
            var plan = await PlanAsync("Sample.Items.Shop.Label", "Sample.Items.Item");

            Assert.That(plan.Refusal, Is.Null);
            var inventory = GetText(plan, "Game/Inventory.cs");
            var moved = "        public void Label(string title)\n        {\n            Title = title;\n"
                + "            Console.WriteLine(Title);\n        }\n";
            Assert.That(inventory, Does.Contain(moved));
            Assert.That(GetText(plan, "Game/Shop.cs"), Does.Contain("=> item.Label(\"x\");"));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
        }

        [TestCase("Sample.Items.Shop.Total", "Sample.Items.Item", "без единственного параметра типа Item")]
        [TestCase("Sample.Items.Shop.Sell", "Sample.Items.Item", "использует экземпляр Shop")]
        [TestCase("Sample.Items.Shop.m_Sold", "Sample.Items.Item", "поле экземпляра")]
        [TestCase("Sample.Units.Player.m_Speed", "Sample.Items.Item", "данные сериализуются")]
        [TestCase("Sample.Items.Pricing.Rate", "Sample.Items.Pricing", "член уже в этом типе")]
        [TestCase("Sample.Items.Inventory.Total", "Sample.Items.Item", "свойство экземпляра")]
        public async Task Refusals(string member, string target, string reason)
        {
            var plan = await PlanAsync(member, target);

            Assert.That(plan.Refusal, Does.Contain(reason));
            Assert.That(plan.Changes, Is.Empty);
        }

        private async Task<RefactorPlan> PlanAsync(string member, string target)
        {
            var symbol = await ResolveAsync(member);
            var type = (INamedTypeSymbol)await ResolveAsync(target);
            return await MemberMover.PlanAsync(m_Solution, symbol, type, Options, true);
        }

        private async Task<ISymbol> ResolveAsync(string name)
        {
            var symbols = await SymbolResolver.ResolveAsync(
                m_Solution,
                SymbolQuery.Parse(name),
                m_Solution.Root,
                false);
            return symbols[0];
        }

        private static string GetText(RefactorPlan plan, string file)
            => plan.Changes.Single(change => change.File == file).After.ToString().Replace("\r\n", "\n");
    }
}
