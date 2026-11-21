using CSR.Workspace.Loading;
using CSR.Workspace.References;
using CSR.Workspace.Symbols;
using Microsoft.CodeAnalysis;

namespace CSR.Workspace.Tests
{
    // Загрузка тестового решения, разбор символа, поиск ссылок
    public class ReferenceTests
    {
        private LoadedSolution m_Solution = null!;

        [OneTimeSetUp]
        public async Task Load()
            => m_Solution = await SampleSolution.LoadSharedAsync();

#region Loading

        [Test]
        public void Load_SdkSolution()
        {
            var projects = m_Solution.Current.Projects.Select(project => project.Name).OrderBy(name => name);

            Assert.That(projects, Is.EqualTo(new[] { "Game", "Game.Tools" }));
            Assert.That(m_Solution.Isolated, Is.False);
            Assert.That(m_Solution.Warnings, Is.Empty);
            Assert.That(m_Solution.Root, Is.EqualTo(Path.GetDirectoryName(m_Solution.Path)));
        }

        [Test]
        public async Task Load_CompilesWithoutErrors()
        {
            foreach (var project in m_Solution.Current.Projects)
            {
                var compilation = await project.GetCompilationAsync();
                var errors = compilation!.GetDiagnostics()
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
                Assert.That(errors, Is.Empty, project.Name);
            }
        }

        [Test]
        public async Task Load_ProjectFilter()
        {
            using var sample = SampleSolution.Create();

            using var tools = await SolutionLoader.LoadAsync(sample.SolutionPath, name => name == "Game.Tools");
            var game = await SolutionLoader.LoadAsync(sample.SolutionPath, name => name == "Game");

            var names = tools.Current.Projects.Select(project => project.Name).OrderBy(name => name);
            Assert.That(names, Is.EqualTo(new[] { "Game", "Game.Tools" }));
            Assert.That(game.Current.Projects.Single().Name, Is.EqualTo("Game"));
            game.Dispose();
            Assert.ThrowsAsync<FileNotFoundException>(() => SolutionLoader.LoadAsync(sample.SolutionPath, _ => false));
        }

#endregion

#region Symbols

        [Test]
        public async Task Resolve_TypeAndField()
        {
            var type = await ResolveAsync("Sample.Items.Inventory");
            var field = await ResolveAsync("Sample.Units.Player.m_Speed");

            Assert.That(type.Single(), Is.InstanceOf<INamedTypeSymbol>());
            Assert.That(field.Single().Name, Is.EqualTo("m_Speed"));
        }

        [Test]
        public async Task Resolve_Overloads()
        {
            var all = await ResolveAsync("Sample.Items.Inventory.Add");
            var one = await ResolveAsync("Sample.Items.Inventory.Add(Item, int)");
            var query = SymbolQuery.Parse("Sample.Items.Inventory.Add");

            Assert.That(all, Has.Count.EqualTo(2));
            Assert.That(((IMethodSymbol)one.Single()).Parameters, Has.Length.EqualTo(2));
            var exception = Assert.ThrowsAsync<SymbolException>(() =>
                SymbolResolver.ResolveAsync(m_Solution, query, m_Solution.Root, false));
            Assert.That(exception!.Message, Does.Contain("Add(Item, int)"));
        }

        [Test]
        public async Task Resolve_Metadata()
        {
            var symbol = await ResolveAsync("System.Console.WriteLine");

            Assert.That(symbol, Is.Not.Empty);
            Assert.That(symbol[0].Locations.Any(location => location.IsInSource), Is.False);
        }

        [Test]
        public void Resolve_NotFound()
        {
            Assert.ThrowsAsync<SymbolException>(() => ResolveAsync("Sample.Items.Missing"));
        }

        [Test]
        public async Task Resolve_PositionWithColumn()
        {
            // Report.cs, строка 12: "target.TakeDamage(1);" - ссылка на метод интерфейса
            var symbol = await ResolveAsync("Game.Tools/Report.cs:12:20");

            Assert.That(SymbolText.Qualified(symbol.Single()), Is.EqualTo("Sample.Units.IDamageable.TakeDamage"));
        }

        [Test]
        public async Task Resolve_PositionWithoutColumn()
        {
            // Player.cs, строка 14: "private float m_Speed = 5;" - объявление, а не тип float
            var symbol = await ResolveAsync("Game/Player.cs:14");

            Assert.That(SymbolText.Qualified(symbol.Single()), Is.EqualTo("Sample.Units.Player.m_Speed"));
        }

        [Test]
        public void Resolve_PositionOutsideFile()
        {
            Assert.ThrowsAsync<SymbolException>(() => ResolveAsync("Game/Player.cs:500:1"));
        }

#endregion

#region References

        [Test]
        public async Task Refs_OverloadsAcrossProjects()
        {
            var report = await FindAsync("Sample.Items.Inventory.Add");

            var places = report.Definitions.SelectMany(definition => definition.References).ToList();
            Assert.That(report.Definitions, Has.Count.EqualTo(2));
            Assert.That(places.Select(place => place.File).Distinct(), Is.EquivalentTo(new[]
            {
                "Game/Inventory.cs", "Game/Player.cs", "Game.Tools/Report.cs",
            }));
            Assert.That(report.ReferenceCount, Is.EqualTo(4));
            Assert.That(places.Single(place => place.File == "Game/Player.cs").Container,
                Is.EqualTo("Sample.Units.Player.Pick(Sample.Items.Item)"));
        }

        [Test]
        public async Task Refs_InterfaceImplementation()
        {
            var report = await FindAsync("Sample.Units.IDamageable.TakeDamage");

            var symbols = report.Definitions.Select(definition => definition.Symbol);
            Assert.That(symbols, Does.Contain("Sample.Units.IDamageable.TakeDamage(int)"));
            Assert.That(symbols, Does.Contain("Sample.Units.Player.TakeDamage(int)"));
            var declaration = report.Definitions.First().Declarations.Single();
            Assert.That(declaration.File, Is.EqualTo("Game/Player.cs"));
            Assert.That(declaration.Text, Is.EqualTo("void TakeDamage(int amount);"));
        }

        [Test]
        public async Task Refs_WritersJsonAndText()
        {
            var report = await FindAsync("Sample.Units.Player.m_Speed");

            var json = ReferenceWriter.WriteJson(report);
            var text = ReferenceWriter.WriteText(report);

            Assert.That(json, Does.Contain("\"query\": \"Sample.Units.Player.m_Speed\""));
            Assert.That(json, Does.Contain("\"file\": \"Game/Player.cs\""));
            Assert.That(json, Does.Not.Contain("\"generated\""));
            Assert.That(text, Does.StartWith("# Sample.Units.Player.m_Speed (field): ссылок 2\n"));
            Assert.That(text, Does.Contain("Game/Player.cs:14:23: объявление: private float m_Speed = 5;"));
            Assert.That(text, Does.Contain("Game/Player.cs:35:31: Nick = nameof(m_Speed);"));
        }

        [Test]
        public async Task Refs_MetadataSymbol()
        {
            var report = await FindAsync("System.Console.WriteLine");

            var places = report.Definitions.SelectMany(definition => definition.References).ToList();
            Assert.That(places.Single().File, Is.EqualTo("Game/Inventory.cs"));
            var referenced = report.Definitions.Single(definition => definition.References.Count > 0);
            Assert.That(referenced.Metadata, Is.True);
        }

#endregion

        private async Task<IReadOnlyList<ISymbol>> ResolveAsync(string text)
            => await SymbolResolver.ResolveAsync(m_Solution, SymbolQuery.Parse(text), m_Solution.Root, true);

        private async Task<ReferenceReport> FindAsync(string text)
        {
            var symbols = await ResolveAsync(text);
            return await ReferenceFinder.FindAsync(m_Solution.Current, m_Solution.Root, text, symbols);
        }
    }
}
