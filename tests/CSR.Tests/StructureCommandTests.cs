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

        // Проект Unity (Assets рядом с ProjectSettings): код без asmdef - предопределенные сборки, asmdef выше - своя
        [Test]
        public void Assemblies_UnityPredefined()
        {
            using var folder = new TempFolder();
            folder.Write("ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.0.0f1");
            folder.Write("Assembly-CSharp.csproj", "<Project />");
            folder.Write("Assembly-CSharp-Editor.csproj", "<Project />");
            folder.Write("Assets/Scripts/Player.cs", "public class Player { }");
            folder.Write("Assets/Scripts/Editor/PlayerEditor.cs", "public class PlayerEditor { }");
            folder.Write("Assets/Plugins/Pool.cs", "public class Pool { }");
            folder.Write("Assets/Plugins/Tools/Editor/PoolEditor.cs", "public class PoolEditor { }");
            folder.Write("Assets/Standard Assets/Camera.cs", "public class FollowCamera { }");
            folder.Write("Assets/Game/Plugins/Local.cs", "public class Local { }");
            folder.Write("Assets/Core/Core.asmdef", "{ \"name\": \"Core\" }");
            folder.Write("Assets/Core/Editor/CoreEditor.cs", "public class CoreEditor { }");
            folder.Write("Assets/Linked/Linked.asmref", "{ \"reference\": \"GUID:0\" }");
            folder.Write("Assets/Linked/Part.cs", "public class Part { }");
            folder.Write("Assets/Linked/Inner/InnerPart.cs", "public class InnerPart { }");
            folder.Write("Assets/Boot.cs", "public class Boot { }");
            folder.Write("Assets/plugins/editor/PluginMenu.cs", "public class PluginMenu { }");
            folder.Write("Packages/Local/Package.cs", "public class Package { }");

            Assert.That(GetAssemblies(folder), Is.EqualTo(new Dictionary<string, int>
            {
                ["Assembly-CSharp"] = 3,
                ["Assembly-CSharp-Editor"] = 1,
                ["Assembly-CSharp-Editor-firstpass"] = 2,
                ["Assembly-CSharp-firstpass"] = 2,
                ["Core"] = 1,
            }));
        }

        // asmdef в самой Assets - весь код в ней; корень проекта с одним .csproj не забирает Packages
        [Test]
        public void Assemblies_UnityAsmdefInAssets()
        {
            using var folder = new TempFolder();
            folder.Write("ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.0.0f1");
            folder.Write("Assembly-CSharp.csproj", "<Project />");
            folder.Write("Assets/Game.asmdef", "{ \"name\": \"Game\" }");
            folder.Write("Assets/Plugins/Editor/Menu.cs", "public class Menu { }");
            folder.Write("Assets/Player.cs", "public class Player { }");
            folder.Write("Packages/Local/Package.cs", "public class Package { }");

            Assert.That(GetAssemblies(folder), Is.EqualTo(new Dictionary<string, int> { ["Game"] = 2 }));
        }

        // Без ProjectSettings папка Assets - обычная: корень с двумя .csproj - вне сборок, как до правил Unity
        [Test]
        public void Assemblies_AssetsWithoutProjectSettings_NoUnityRules()
        {
            using var folder = new TempFolder();
            folder.Write("One.csproj", "<Project />");
            folder.Write("Two.csproj", "<Project />");
            folder.Write("Assets/Scripts/Player.cs", "public class Player { }");
            folder.Write("Tools/Tools.csproj", "<Project />");
            folder.Write("Tools/Editor/Helper.cs", "public class Helper { }");

            Assert.That(GetAssemblies(folder), Is.EqualTo(new Dictionary<string, int> { ["Tools"] = 1 }));
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

        // Сборки отчета: имя -> число типов
        private static Dictionary<string, int> GetAssemblies(TempFolder folder)
        {
            var config = folder.Write("structure.jsonc", """{ "minAssemblyLines": 1 }""");
            var result = Cli.Run("structure", folder.Path, "--config", config);
            Assert.That(result.ExitCode, Is.EqualTo(0), result.Error);
            using var document = JsonDocument.Parse(result.Output);
            return document.RootElement.GetProperty("assemblies").GetProperty("items").EnumerateArray()
                .ToDictionary(item => item.GetProperty("name").GetString()!, item => item.GetProperty("types").GetInt32());
        }
    }
}
