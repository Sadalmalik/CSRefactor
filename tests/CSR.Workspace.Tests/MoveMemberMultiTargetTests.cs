using CSR.Workspace.Loading;
using CSR.Workspace.Refactor;
using CSR.Workspace.Symbols;
using Microsoft.CodeAnalysis;

namespace CSR.Workspace.Tests
{
    // csr move члена в проекте с двумя платформами: файл есть в двух проектах решения, ссылка приходит дважды
    public class MoveMemberMultiTargetTests
    {
        private const string Project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework></TargetFramework>
                <TargetFrameworks>net10.0;netstandard2.0</TargetFrameworks>
                <LangVersion>latest</LangVersion>
              </PropertyGroup>
            </Project>

            """;

        private const string Rates = """
            namespace Sample.Items
            {
                public static class Rates
                {
                    public static int Rate()
                        => 2;

                    public static int Double()
                        => Rate() * 2;
                }

                public static class Prices
                {
                    public static int Get(int count)
                        => count * Rates.Rate();
                }
            }

            """;

        private SampleSolution m_Sample = null!;
        private LoadedSolution m_Solution = null!;

        [OneTimeSetUp]
        public async Task Load()
        {
            m_Sample = SampleSolution.Create();
            File.WriteAllText(m_Sample.GetPath("Game/Game.csproj"), Project);
            File.WriteAllText(m_Sample.GetPath("Game/Rates.cs"), Rates);
            m_Sample.Restore();
            m_Solution = await m_Sample.LoadAsync();
        }

        [OneTimeTearDown]
        public void Unload()
        {
            m_Solution.Dispose();
            m_Sample.Dispose();
        }

        [Test]
        public async Task Static_EachReferenceOnce_BothPlatformsCompile()
        {
            var member = await ResolveAsync("Sample.Items.Rates.Rate");
            var target = (INamedTypeSymbol)await ResolveAsync("Sample.Items.Inventory");

            var plan = await MemberMover.PlanAsync(m_Solution, member, target, new RefactorOptions(), true);

            Assert.That(plan.Refusal, Is.Null);
            var rates = GetText(plan, "Game/Rates.cs");
            Assert.That(rates, Does.Contain("=> Inventory.Rate() * 2;"));
            Assert.That(rates, Does.Contain("=> count * Inventory.Rate();"));
            Assert.That(rates, Does.Not.Contain("public static int Rate()"));
            Assert.That(GetText(plan, "Game/Inventory.cs"), Does.Contain("public static int Rate()"));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
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
