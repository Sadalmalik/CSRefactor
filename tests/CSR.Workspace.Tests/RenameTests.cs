using System.Text;
using CSR.Workspace.Loading;
using CSR.Workspace.Rename;
using CSR.Workspace.Symbols;

namespace CSR.Workspace.Tests
{
    // Переименование в памяти на общей копии тестового решения; запись - на своей копии
    public class RenameTests
    {
        private static readonly RefactorOptions Options = new() { SerializationAttributes = ["Sample.Storage.Saved"] };

        private LoadedSolution m_Solution = null!;

        [OneTimeSetUp]
        public async Task Load()
            => m_Solution = await SampleSolution.LoadSharedAsync();

#region Plain

        [Test]
        public async Task Rename_PrivateField()
        {
            var plan = await PlanAsync("Sample.Storage.SaveData.m_Plain", "m_Value");

            Assert.That(plan.SerializationRefused, Is.False);
            Assert.That(plan.Changes.Select(change => change.File), Is.EqualTo(new[] { "Game/SaveData.cs" }));
            Assert.That(plan.Diff, Does.Contain("-        private int m_Plain;\n+        private int m_Value;\n"));
            Assert.That(plan.Diff, Does.Contain("-            => m_Plain;\n+            => m_Value;\n"));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
            Assert.That(plan.Strings!.Total, Is.Zero);
        }

        [Test]
        public async Task Rename_TypeAcrossProjectsWithStrings()
        {
            var plan = await PlanAsync("Sample.Items.Inventory", "Bag");

            Assert.That(plan.Changes.Select(change => change.File), Is.EqualTo(new[]
            {
                "Game.Tools/Report.cs", "Game/Inventory.cs", "Game/Player.cs",
            }));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
            Assert.That(plan.Compile.Projects, Is.EqualTo(2));
            Assert.That(plan.Notes.Single(), Does.StartWith("файл Game/Inventory.cs назван по типу"));
            var literals = plan.Strings!.Matches.Where(match => match.Kind == "literal").Select(match => match.File);
            Assert.That(literals, Is.EquivalentTo(new[] { "Game.Tools/Report.cs", "Game/Inventory.cs" }));
        }

        [Test]
        public async Task Rename_NameofIsNotStringMatch()
        {
            var plan = await PlanAsync("Sample.Units.Player.m_Speed", "m_Velocity", migrate: true);

            Assert.That(plan.Diff, Does.Contain("+                Nick = nameof(m_Velocity);"));
            Assert.That(plan.Strings!.Matches.Where(match => match.Kind == "literal"), Is.Empty);
        }

        [Test]
        public async Task Rename_MethodFromPositionRenamesImplementation()
        {
            var plan = await PlanAsync("Game.Tools/Report.cs:12:20", "ApplyDamage");

            var files = plan.Changes.Select(change => change.File);
            Assert.That(files, Is.EqualTo(new[] { "Game.Tools/Report.cs", "Game/Player.cs" }));
            Assert.That(plan.Diff, Does.Contain("+        void ApplyDamage(int amount);"));
            Assert.That(plan.Diff, Does.Contain("+        public void ApplyDamage(int amount)"));
        }

        [Test]
        public async Task Rename_ConflictGivesNewErrors()
        {
            var plan = await PlanAsync("Sample.Storage.SaveData.Gold", "Rank");

            Assert.That(plan.HasNewErrors, Is.True);
            Assert.That(plan.Compile!.NewErrors[0].Text, Does.StartWith("CS0102"));
        }

        [Test]
        public void Rename_InvalidName()
        {
            Assert.ThrowsAsync<SymbolException>(() => PlanAsync("Sample.Storage.SaveData.m_Plain", "class"));
            Assert.ThrowsAsync<SymbolException>(() => PlanAsync("Sample.Storage.SaveData.m_Plain", "1x"));
            Assert.ThrowsAsync<SymbolException>(() => PlanAsync("Sample.Storage.SaveData.m_Plain", "m_Plain"));
            Assert.ThrowsAsync<SymbolException>(() => PlanAsync("System.Console.WriteLine", "Print"));
        }

#endregion

#region Serialization

        [Test]
        public async Task Unity_SerializeFieldRefused()
        {
            var plan = await PlanAsync("Sample.Units.Player.m_Speed", "m_Velocity");

            Assert.That(plan.SerializationRefused, Is.True);
            Assert.That(plan.Serialized.Single().Kind, Is.EqualTo(SerializedKind.UnityField));
            Assert.That(plan.Changes, Is.Empty);
            var summary = RenameReport.WriteSummary(plan, RenameStatus.Refused);
            Assert.That(summary, Does.Contain("[FormerlySerializedAs(\"m_Speed\")]"));
        }

        [Test]
        public async Task Unity_MigrateAddsFormerlySerializedAs()
        {
            var plan = await PlanAsync("Sample.Units.Player.m_Speed", "m_Velocity", migrate: true);

            Assert.That(plan.SerializationRefused, Is.False);
            Assert.That(plan.Diff, Does.Contain("+using UnityEngine.Serialization;\n"));
            var attributes = "+        [FormerlySerializedAs(\"m_Speed\")]\n+        [SerializeField]";
            Assert.That(plan.Diff, Does.Contain(attributes));
            Assert.That(plan.Diff, Does.Contain("+        private float m_Velocity = 5;\n"));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
            var files = plan.Strings!.Matches.Where(match => match.Kind == "file").Select(match => match.File);
            Assert.That(files, Is.EquivalentTo(new[] { "Assets/Player.prefab", "Scripts/player.lua" }));
        }

        [Test]
        public async Task Unity_PublicFieldOfMonoBehaviour()
        {
            var plan = await PlanAsync("Sample.Units.Player.Health", "Hp", migrate: true);

            Assert.That(plan.Serialized.Single().Kind, Is.EqualTo(SerializedKind.UnityField));
            Assert.That(plan.Diff, Does.Contain("+        [FormerlySerializedAs(\"Health\")]\n"));
        }

        [Test]
        public async Task Unity_AutoPropertyBackingField()
        {
            var plan = await PlanAsync("Sample.Units.Player.Nick", "Name", migrate: true);

            Assert.That(plan.Serialized.Single().BackingField, Is.True);
            Assert.That(plan.Diff, Does.Contain("+        [field: FormerlySerializedAs(\"<Nick>k__BackingField\")]\n"));
            Assert.That(plan.Compile!.NewErrors, Is.Empty);
        }

        [Test]
        public async Task Unity_SerializableFields()
        {
            var title = await PlanAsync("Sample.Items.Item.Title", "Caption");
            var cache = await PlanAsync("Sample.Items.Item.Cache", "Buffer");
            var width = await PlanAsync("Sample.Items.Item.Width", "SizeX", migrate: true);

            Assert.That(title.SerializationRefused, Is.True);
            Assert.That(cache.Serialized, Is.Empty);
            Assert.That(width.SerializationRefused, Is.True);
            Assert.That(width.Serialized.Single().MigrationProblem, Does.Contain("несколько переменных"));
        }

        [Test]
        public async Task Unity_MessageAlwaysRefused()
        {
            var plan = await PlanAsync("Sample.Units.Player.Update", "Tick", migrate: true);

            Assert.That(plan.SerializationRefused, Is.True);
            Assert.That(plan.Serialized.Single().Kind, Is.EqualTo(SerializedKind.UnityMessage));
        }

        [Test]
        public async Task Json_PropertyWithoutName()
        {
            var refused = await PlanAsync("Sample.Storage.SaveData.Level", "Stage");
            var migrated = await PlanAsync("Sample.Storage.SaveData.Level", "Stage", migrate: true);
            var named = await PlanAsync("Sample.Storage.SaveData.Gold", "Coins");
            var ordered = await PlanAsync("Sample.Storage.SaveData.Rank", "Grade", migrate: true);

            Assert.That(refused.Serialized.Single().Kind, Is.EqualTo(SerializedKind.Json));
            Assert.That(migrated.Diff, Does.Contain("+        [JsonProperty(\"Level\")]\n+        public int Stage;"));
            Assert.That(named.Serialized, Is.Empty);
            Assert.That(ordered.Diff, Does.Contain("+        [JsonProperty(\"Rank\", Order = 2)]\n"));
        }

        [Test]
        public async Task DataMember_WithoutName()
        {
            var plan = await PlanAsync("Sample.Storage.SaveData.Name", "Title", migrate: true);

            Assert.That(plan.Serialized.Single().Kind, Is.EqualTo(SerializedKind.DataMember));
            Assert.That(plan.Diff, Does.Contain("+        [DataMember(Name = \"Name\")]\n"));
        }

        [Test]
        public async Task Custom_AttributeFromConfig()
        {
            var plan = await PlanAsync("Sample.Storage.SaveData.Slot", "Index", migrate: true);
            var json = RenameReport.WriteJson(plan, RenameStatus.Refused);

            Assert.That(plan.SerializationRefused, Is.True);
            Assert.That(plan.Serialized.Single().Kind, Is.EqualTo(SerializedKind.Custom));
            Assert.That(json, Does.Contain("\"status\": \"refused\""));
            Assert.That(json, Does.Contain("[Sample.Storage.Saved]"));
        }

#endregion

#region Apply

        [Test]
        public async Task Apply_WritesFilesAndKeepsBom()
        {
            using var sample = SampleSolution.Create();
            var inventory = sample.GetPath("Game/Inventory.cs");
            File.WriteAllText(inventory, File.ReadAllText(inventory), new UTF8Encoding(true));
            using var loaded = await sample.LoadAsync();
            var query = SymbolQuery.Parse("Sample.Items.Inventory");
            var symbols = await SymbolResolver.ResolveAsync(loaded, query, loaded.Root, false);
            var plan = await RenameEngine.PlanAsync(loaded, symbols[0], "Bag", new RenameSettings(), Options);

            RenameEngine.Apply(loaded, plan);

            var bom = new byte[] { 0xEF, 0xBB, 0xBF };
            var report = File.ReadAllText(sample.GetPath("Game.Tools/Report.cs"));
            var player = File.ReadAllBytes(sample.GetPath("Game/Player.cs"));
            var current = await loaded.FindDocuments(inventory)[0].GetTextAsync();
            Assert.That(File.ReadAllBytes(inventory).Take(3), Is.EqualTo(bom));
            Assert.That(File.ReadAllText(inventory), Does.Contain("public class Bag"));
            Assert.That(report, Does.Contain("Describe(Bag inventory"));
            Assert.That(player.Take(3), Is.Not.EqualTo(bom));
            Assert.That(current.ToString(), Does.Contain("public class Bag"));
        }

#endregion

        private async Task<RenamePlan> PlanAsync(string symbol, string newName, bool migrate = false)
        {
            var query = SymbolQuery.Parse(symbol);
            var symbols = await SymbolResolver.ResolveAsync(m_Solution, query, m_Solution.Root, false);
            var settings = new RenameSettings { Migrate = migrate };
            return await RenameEngine.PlanAsync(m_Solution, symbols[0], newName, settings, Options);
        }
    }
}
