namespace CSR.Workspace.Tests.Arch
{
    // csr arch --solution через Program.Main без сервера: признаки по файлам и по решению на тестовом решении
    public class ArchSolutionCliTests
    {
        [Test]
        public void Solution_ProjectMaskAndMechanism()
        {
            using var sample = SampleSolution.Create();
            var config = Path.Combine(sample.Root, "arch.jsonc");
            File.WriteAllText(config, """
                {
                    "checks": {
                        "project-mechanism": { "severity": "warning", "rule": "A5" },
                        "variant-flag": { "severity": "hint", "minFiles": 1 }
                    }
                }
                """);
            var project = Path.Combine(sample.Root, "project.jsonc");
            File.WriteAllText(project, """{ "logger": { "methods": ["Log.Info"], "bypass": ["Console.Write*"] } }""");

            var result = Cli.Run("arch", "--solution", sample.SolutionPath, "--projects", "Game", "--no-server",
                "--config", config, "--project", project, "--format", "md");
            var all = Cli.Run("arch", "--solution", sample.SolutionPath, "--no-server", "--config", config,
                "--project", project);

            Assert.That(result.ExitCode, Is.Zero, result.Error);
            Assert.That(result.Output, Does.Contain("режим: solution"));
            const string mechanism = "`project-mechanism` A5 (warning) - 'Console.WriteLine' в обход 'logger'";
            Assert.That(result.Output, Does.Contain(mechanism));
            Assert.That(result.Error, Does.Contain("csr arch: проектов 1,"));
            Assert.That(all.ExitCode, Is.Zero, all.Error);
            Assert.That(all.Error, Does.Contain("csr arch: проектов 2,"));
            Assert.That(all.Output, Does.Contain("\"scope\": \"solution\""));
        }

        [Test]
        public void Solution_InvalidArguments_ExitTwo()
        {
            using var sample = SampleSolution.Create();
            var config = Path.Combine(sample.Root, "arch.jsonc");
            File.WriteAllText(config, """{ "checks": { "variant-flag": {} } }""");
            var solution = sample.SolutionPath;

            var files = Cli.Run("arch", sample.Root, "--solution", solution, "--no-server", "--config", config);
            var diff = Cli.Run("arch", "--solution", solution, "--no-server", "--config", config, "--diff", "HEAD");
            var projects = Cli.Run("arch", "--solution", solution, "--no-server", "--config", config,
                "--projects", "Missing*");

            Assert.That(files.ExitCode, Is.EqualTo(2));
            Assert.That(files.Error, Does.Contain("файлы не задаются"));
            Assert.That(diff.ExitCode, Is.EqualTo(2));
            Assert.That(projects.ExitCode, Is.EqualTo(2));
            Assert.That(projects.Error, Does.Contain("нет проектов по маске: Missing*"));
        }
    }
}
