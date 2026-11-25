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

        // Вложенная сборка: файлы папки Tools - в Tools, а не в Core; папка с двумя .csproj (корень Unity) - вне сборок;
        // слой - все сборки
        [Test]
        public void Assemblies_NestedManifestAndLayerDepth()
        {
            using var folder = new TempFolder();
            folder.Write("Core/Core.csproj", "<Project />");
            folder.Write("Core/Engine.cs", "public class Engine { private Helper m_Helper; }");
            folder.Write("Core/Tools/Tools.asmdef", "{ \"name\": \"Tools\" }");
            folder.Write("Core/Tools/Helper.cs", "public class Helper { }");
            folder.Write("Game/Game.csproj", "<Project />");
            folder.Write("Game/Player.cs", "public class Player { private Engine m_Engine; }");
            folder.Write("Unity/One.csproj", "<Project />");
            folder.Write("Unity/Two.csproj", "<Project />");
            folder.Write("Unity/Script.cs", "public class Script { private Engine m_Engine; }");
            var config = folder.Write("structure.jsonc", """
                { "minAssemblyLines": 2, "layerDepth": 1, "layers": { "All": ["*"] } }
                """);
            var result = Cli.Run("structure", folder.Path, "--config", config);

            Assert.That(result.ExitCode, Is.EqualTo(0), result.Error);
            using var document = JsonDocument.Parse(result.Output);
            var assemblies = document.RootElement.GetProperty("assemblies").GetProperty("items");
            Assert.That(
                assemblies.EnumerateArray().Select(item => item.GetProperty("name").GetString()),
                Is.EqualTo(new[] { "Core", "Game", "Tools" }));
            Assert.That(assemblies[0].GetProperty("dependencies")[0].GetString(), Is.EqualTo("Tools"));
            Assert.That(assemblies[0].GetProperty("small").GetBoolean(), Is.True);
            var layer = document.RootElement.GetProperty("layers").GetProperty("items")[0];
            Assert.That(layer.GetProperty("depth").GetInt32(), Is.EqualTo(2));
            Assert.That(layer.GetProperty("deep").GetBoolean(), Is.True);

            var markdown = Cli.Run("structure", folder.Path, "--config", config, "--format", "md").Output;
            Assert.That(markdown, Does.Contain("сборок меньше 2 строк - 3, слоев глубже 1 - 1."));
            Assert.That(markdown, Does.Contain("- `All`: 2 **глубже порога**: Game -> Core -> Tools"));
        }

        [Test]
        public void Layers_ValueNotArray_ExitTwo()
        {
            using var folder = new TempFolder();
            folder.Write("Engine.cs", "public class Engine { }");
            var config = folder.Write("structure.jsonc", """{ "layers": { "All": "*" } }""");
            var result = Cli.Run("structure", folder.Path, "--config", config);
            Assert.That(result.ExitCode, Is.EqualTo(2));
            Assert.That(result.Error, Does.Contain("layers.All: ожидается массив строк"));
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
