namespace CSR.Workspace.Tests
{
    public class CliResult
    {
        public required int ExitCode { get; init; }

        public required string Output { get; init; }

        public required string Error { get; init; }
    }

    public static class Cli
    {
        // Program.Main с перехватом stdout и stderr; тесты идут последовательно (консоль общая)
        public static CliResult Run(params string[] args)
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;
            var output = new StringWriter();
            var error = new StringWriter();
            Console.SetOut(output);
            Console.SetError(error);
            try
            {
                var code = Program.Main(args);
                return new CliResult { ExitCode = code, Output = output.ToString(), Error = error.ToString() };
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }
    }

    // refs и rename через Program.Main без сервера: загрузка решения в процессе
    public class CliTests
    {
        [Test]
        public void Help()
        {
            Assert.That(Cli.Run("help").Output, Does.Contain("serve    держать решение загруженным"));
            Assert.That(Cli.Run("help", "refs").Output, Does.StartWith("csr refs <символ>"));
            Assert.That(Cli.Run("help", "rename").Output, Does.Contain("--migrate"));
            Assert.That(Cli.Run("help", "serve").Output, Does.Contain("--detach"));
        }

        [Test]
        public void Refs_JsonAndErrors()
        {
            using var sample = SampleSolution.Create();

            var json = Cli.Run("refs", "Sample.Items.Inventory.Add", "--solution", sample.SolutionPath, "--no-server");
            var missing = Cli.Run("refs", "Sample.Items.Missing", "--solution", sample.SolutionPath, "--no-server");
            var format = Cli.Run(
                "refs",
                "Sample.Items.Inventory",
                "--solution",
                sample.SolutionPath,
                "--format",
                "xml");
            var noSolution = Cli.Run("refs", "X.Y", "--solution", sample.GetPath("None.sln"));

            Assert.That(json.ExitCode, Is.Zero, json.Error);
            Assert.That(json.Output, Does.Contain("\"references\": 4"));
            Assert.That(json.Error, Does.Contain("csr refs: ссылок 4 в 3 файлах"));
            Assert.That(missing.ExitCode, Is.EqualTo(2));
            Assert.That(missing.Error, Does.Contain("не найден"));
            Assert.That(format.ExitCode, Is.EqualTo(2));
            Assert.That(noSolution.ExitCode, Is.EqualTo(2));
        }

        [Test]
        public void Rename_PreviewRefuseBlockApply()
        {
            using var sample = SampleSolution.Create();
            var config = Path.Combine(sample.Root, "refactor.jsonc");
            File.WriteAllText(config, """{ "strings": { "extensions": [".lua"] } }""");
            var solution = sample.SolutionPath;
            var inventory = File.ReadAllText(sample.GetPath("Game/Inventory.cs"));

            var preview = Cli.Run("rename", "Sample.Items.Inventory", "Bag", "--solution", solution, "--no-server");
            var refused = Cli.Run(
                "rename",
                "Sample.Units.Player.m_Speed",
                "m_Velocity",
                "--solution",
                solution,
                "--no-server",
                "--format",
                "json",
                "--config",
                config);
            var blocked = Cli.Run(
                "rename",
                "Sample.Items.Inventory",
                "Bag",
                "--solution",
                solution,
                "--no-server",
                "--apply");
            var unchanged = File.ReadAllText(sample.GetPath("Game/Inventory.cs"));
            var applied = Cli.Run(
                "rename",
                "Sample.Items.Inventory",
                "Bag",
                "--solution",
                solution,
                "--no-server",
                "--apply",
                "--force",
                "--no-verify");

            Assert.That(preview.ExitCode, Is.Zero, preview.Error);
            Assert.That(preview.Output, Does.StartWith("--- a/Game.Tools/Report.cs\n+++ b/Game.Tools/Report.cs\n"));
            Assert.That(preview.Error, Does.Contain("проверить вручную: строковых совпадений"));
            Assert.That(refused.ExitCode, Is.EqualTo(1));
            Assert.That(refused.Output, Does.Contain("\"status\": \"refused\""));
            Assert.That(blocked.ExitCode, Is.EqualTo(1));
            Assert.That(blocked.Error, Does.Contain("не записано: есть строковые совпадения"));
            Assert.That(unchanged, Is.EqualTo(inventory));
            Assert.That(applied.ExitCode, Is.Zero, applied.Error);
            Assert.That(File.ReadAllText(sample.GetPath("Game/Inventory.cs")), Does.Contain("public class Bag"));
        }

        [Test]
        public void Rename_FindsSolutionFromFilePosition()
        {
            using var sample = SampleSolution.Create();
            var file = sample.GetPath("Game/SaveData.cs");

            var result = Cli.Run("rename", file + ":23", "m_Value", "--no-server", "--no-strings");

            Assert.That(result.ExitCode, Is.Zero, result.Error);
            Assert.That(result.Output, Does.Contain("+        private int m_Value;"));
        }

        [Test]
        public void Move_PreviewRefuseApply()
        {
            using var sample = SampleSolution.Create();
            var solution = sample.SolutionPath;

            var preview = Cli.Run("move", "Sample.Items.Item", "--solution", solution, "--no-server");
            var refused = Cli.Run("move", "Sample.Items.Inventory", "--solution", solution, "--no-server");
            var notType = Cli.Run("move", "Sample.Items.Inventory.Add", "--solution", solution, "--no-server");
            var applied = Cli.Run(
                "move",
                "Sample.Units.IDamageable",
                "--solution",
                solution,
                "--no-server",
                "--apply",
                "--format",
                "json");

            Assert.That(preview.ExitCode, Is.Zero, preview.Error);
            Assert.That(preview.Output, Does.Contain("--- /dev/null\n+++ b/Game/Item.cs\n"));
            Assert.That(File.Exists(sample.GetPath("Game/Item.cs")), Is.False);
            Assert.That(refused.ExitCode, Is.EqualTo(1));
            Assert.That(refused.Error, Does.Contain("отказ: тип уже в файле со своим именем"));
            Assert.That(notType.ExitCode, Is.EqualTo(2));
            Assert.That(applied.ExitCode, Is.Zero, applied.Error);
            Assert.That(applied.Output, Does.Contain("\"status\": \"applied\""));
            Assert.That(File.ReadAllText(sample.GetPath("Game/IDamageable.cs")), Does.Contain("interface IDamageable"));
            Assert.That(File.ReadAllText(sample.GetPath("Game/Player.cs")), Does.Not.Contain("interface IDamageable"));
        }

        [Test]
        public void Move_MemberAcrossProjectsWithUsings()
        {
            using var sample = SampleSolution.Create();
            var solution = sample.SolutionPath;

            var noTarget = Cli.Run("move", "Sample.Tools.Report.Describe", "--solution", solution, "--no-server");
            var applied = Cli.Run(
                "move",
                "Sample.Tools.Report.Describe",
                "--to",
                "Sample.Items.Inventory",
                "--solution",
                solution,
                "--no-server",
                "--apply");

            Assert.That(noTarget.ExitCode, Is.EqualTo(2));
            Assert.That(noTarget.Error, Does.Contain("нужен --to"));
            Assert.That(applied.ExitCode, Is.Zero, applied.Error);
            var inventory = File.ReadAllText(sample.GetPath("Game/Inventory.cs"));
            Assert.That(inventory, Does.StartWith("using Sample.Units;"));
            Assert.That(
                inventory,
                Does.Contain("public static string Describe(Inventory inventory, IDamageable target)"));
            var report = File.ReadAllText(sample.GetPath("Game.Tools/Report.cs"));
            Assert.That(report, Does.Not.Contain("using Sample.Units;"));
            Assert.That(report, Does.Not.Contain("Describe"));
        }

        [Test]
        public void Extract_FindsSolutionFromFileRange()
        {
            using var sample = SampleSolution.Create();
            var file = sample.GetPath("Game/Inventory.cs");

            var result = Cli.Run("extract", file + ":27-28", "Store", "--no-server");
            var invalid = Cli.Run("extract", file + ":27", "Store", "--no-server");

            Assert.That(result.ExitCode, Is.Zero, result.Error);
            Assert.That(result.Output, Does.Contain("+            Store(item, count);"));
            Assert.That(result.Output, Does.Contain("+        private void Store(Item item, int count)"));
            Assert.That(invalid.ExitCode, Is.EqualTo(2));
            Assert.That(invalid.Error, Does.Contain("файл.cs:строка-строка"));
        }
    }
}
