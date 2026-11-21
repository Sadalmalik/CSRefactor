using CSR.Commands;
using CSR.Workspace.Server;

namespace CSR.Workspace.Tests
{
    // csr serve в процессе теста: канал, правки на диске, перезагрузка, остановка; CLI находит сервер сам
    public class ServerTests
    {
        private const string ExtraFile = """
            namespace Sample.Items
            {
                public static class Extra
                {
                    public static void Fill(Inventory inventory)
                        => inventory.Add(new Item());
                }
            }
            """;

        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

        [Test]
        public async Task Server_ServesCommandsAndFollowsDisk()
        {
            using var sample = SampleSolution.Create();
            var loaded = await sample.LoadAsync();
            var server = new WorkspaceServer(loaded, WorkspaceCommands.ExecuteAsync, TimeSpan.Zero, TextWriter.Null);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var running = server.RunAsync(cancellation.Token);

            var status = await PingAsync(sample);
            Assert.That(status!.Output, Does.Contain("проектов: 2"));
            Assert.That(ServerRegistry.List().Select(entry => entry.Solution), Does.Contain(sample.SolutionPath));
            Assert.That(await CountRefsAsync(sample), Is.EqualTo(4));

            // правка существующего файла: текст перечитывается без перезагрузки
            var player = sample.GetPath("Game/Player.cs");
            File.WriteAllText(player, File.ReadAllText(player).Replace(
                "=> m_Inventory.Add(item);",
                "{\n            m_Inventory.Add(item);\n            m_Inventory.Add(item, 2);\n        }"));
            Assert.That(await WaitForAsync(() => CountRefsAsync(sample), count => count == 5), Is.EqualTo(5));
            Assert.That(server.Reloads, Is.Zero);

            // новый файл в проекте SDK: перезагрузка решения
            File.WriteAllText(sample.GetPath("Game/Extra.cs"), ExtraFile);
            Assert.That(await WaitForAsync(() => CountRefsAsync(sample), count => count == 6), Is.EqualTo(6));
            Assert.That(server.Reloads, Is.EqualTo(1));

            var timeout = TimeSpan.FromSeconds(5);
            var stop = await WorkspaceClient.SendServiceAsync(sample.SolutionPath, WorkspaceProtocol.Stop, timeout);
            Assert.That(stop!.ExitCode, Is.Zero);
            await running.WaitAsync(Wait);
            Assert.That(ServerRegistry.List().Select(entry => entry.Solution), Does.Not.Contain(sample.SolutionPath));
        }

        [Test]
        public async Task Cli_UsesRunningServer()
        {
            using var sample = SampleSolution.Create();
            var loaded = await sample.LoadAsync();
            var server = new WorkspaceServer(loaded, WorkspaceCommands.ExecuteAsync, TimeSpan.Zero, TextWriter.Null);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var running = server.RunAsync(cancellation.Token);
            await PingAsync(sample);

            var refs = Cli.Run(
                "refs",
                "Sample.Units.Player.m_Speed",
                "--solution",
                sample.SolutionPath,
                "--format",
                "text");
            var status = Cli.Run("serve", sample.SolutionPath, "--status");
            var stop = Cli.Run("serve", sample.SolutionPath, "--stop");
            await running.WaitAsync(Wait);
            var stopped = Cli.Run("serve", sample.SolutionPath, "--status");

            Assert.That(refs.ExitCode, Is.Zero, refs.Error);
            Assert.That(refs.Output, Does.Contain("Game/Player.cs:14:23: объявление"));
            Assert.That(refs.Error, Does.Not.Contain("загрузка"));
            Assert.That(status.Output, Does.Contain("канал: csr-"));
            Assert.That(stop.ExitCode, Is.Zero);
            Assert.That(stopped.ExitCode, Is.EqualTo(1));
        }

        private static Task<WorkspaceResponse?> PingAsync(SampleSolution sample)
        {
            var timeout = TimeSpan.FromSeconds(1);
            var solution = sample.SolutionPath;
            return WaitForAsync(
                () => WorkspaceClient.SendServiceAsync(solution, WorkspaceProtocol.Ping, timeout),
                response => response != null);
        }

        private static async Task<int> CountRefsAsync(SampleSolution sample)
        {
            var request = new WorkspaceRequest
            {
                Command = "refs",
                Arguments = ["Sample.Items.Inventory.Add", "--format", "text"],
                WorkingDirectory = sample.Root,
            };
            var response = await WorkspaceClient.TrySendAsync(sample.SolutionPath, request, TimeSpan.FromSeconds(5));
            Assert.That(response!.ExitCode, Is.Zero, response.Error);
            return response.Output.Split('\n').Count(line => line.Contains(".cs:") && !line.Contains("объявление"));
        }

        private static async Task<T> WaitForAsync<T>(Func<Task<T>> probe, Func<T, bool> done)
        {
            var deadline = DateTime.UtcNow + Wait;
            while (true)
            {
                var value = await probe();
                if (done(value) || DateTime.UtcNow > deadline)
                    return value;

                await Task.Delay(200);
            }
        }
    }
}
