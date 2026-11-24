using System.Text.Json;

namespace CSR.Tests
{
    public class StructureCommandTests
    {
        [Test]
        public void Json_OwnershipAndCycles()
        {
            using var folder = new TempFolder();
            folder.Write("Core/Engine.cs", "public class Engine { private Wheel m_Wheel; }");
            folder.Write("Parts/Wheel.cs", "public class Wheel { private Engine m_Owner; }");
            folder.Write("Parts/Motion.cs", """
                using System.Collections.Generic;
                public abstract class Motion { }
                public class Walk : Motion { }
                public class Run : Motion { }
                public class Agent { private List<Motion> m_Motions; }
                """);
            var config = folder.Write("structure.jsonc", """{ "ownershipDepth": 0, "top": 3 }""");
            var result = Cli.Run("structure", folder.Path, "--config", config);

            Assert.That(result.ExitCode, Is.EqualTo(0), result.Error);
            using var document = JsonDocument.Parse(result.Output);
            var root = document.RootElement;
            Assert.That(root.GetProperty("types").GetInt32(), Is.EqualTo(6));
            var chain = root.GetProperty("ownership").GetProperty("chains")[0];
            Assert.That(chain.GetProperty("root").GetProperty("name").GetString(), Is.EqualTo("Agent"));
            Assert.That(chain.GetProperty("deep").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("folderCycles").GetArrayLength(), Is.EqualTo(1));
        }

        [Test]
        public void Markdown_Summary()
        {
            using var folder = new TempFolder();
            folder.Write("Engine.cs", "public class Engine { }");
            var result = Cli.Run("structure", folder.Path, "--format", "md");

            Assert.That(result.ExitCode, Is.EqualTo(0), result.Error);
            Assert.That(result.Output, Does.StartWith("# csr structure"));
            Assert.That(result.Output, Does.Contain("Отметки: владение глубже 3 - 0, наследование глубже 4 - 0"));
        }

        // --top: целое >= 0, 0 - все; текст ошибки - как у metrics и similar
        [Test]
        public void Top_ZeroAllAndNegativeError()
        {
            using var folder = new TempFolder();
            folder.Write("Engine.cs", "public class Engine { }");

            Assert.That(Cli.Run("structure", folder.Path, "--top", "0").ExitCode, Is.EqualTo(0));
            foreach (var command in new[] { "structure", "metrics", "similar" })
            {
                var result = Cli.Run(command, folder.Path, "--top", "-1");
                Assert.That(result.ExitCode, Is.EqualTo(2), command);
                Assert.That(result.Error, Does.Contain("--top: ожидается неотрицательное целое, задано '-1'"), command);
            }
        }

        [Test]
        public void UnknownConfigKey_ExitTwo()
        {
            using var folder = new TempFolder();
            folder.Write("Engine.cs", "public class Engine { }");
            var config = folder.Write("structure.jsonc", """{ "depth": 3 }""");
            var result = Cli.Run("structure", folder.Path, "--config", config);
            Assert.That(result.ExitCode, Is.EqualTo(2));
            Assert.That(result.Error, Does.Contain("неизвестный ключ 'depth'"));
        }
    }
}
