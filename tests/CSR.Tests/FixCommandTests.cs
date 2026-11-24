using System.Text;
using System.Text.Json;

namespace CSR.Tests
{
    // csr fix через Program.Main: дифф без записи, --apply, json, фильтры, changed-lines
    public class FixCommandTests
    {
        private const string Config = """
            {
              "checks": {
                "null-check": { "severity": "error", "rule": "I12", "prefer": "equality" },
                "xml-doc": { "severity": "error", "rule": "C8" },
                "line-length": { "severity": "error", "rule": "F20", "max": 120 },
                "commented-code": { "severity": "hint", "rule": "C18" },
              },
            }
            """;

        private const string Dirty = """
            public class Sample
            {
                /// <summary>Документация</summary>
                private bool Check(object a, object b)
                {
                    // return a.Equals(b);
                    return a is null || b is null;
                }
            }

            """;

        [Test]
        public void Fix_DiffWithoutWriting()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Dirty);
            var config = folder.Write("style.jsonc", Config);

            var result = Cli.Run("fix", file, "--config", config);

            Assert.That(result.ExitCode, Is.Zero, result.Error);
            Assert.That(result.Output, Does.Contain("-    /// <summary>Документация</summary>\n"));
            Assert.That(result.Output, Does.Contain("+        return a == null || b == null;\n"));
            Assert.That(result.Output, Does.Not.Contain("-        // return"), "hint без --hints не правится");
            Assert.That(result.Error, Does.Contain("исправлено 3 в 1 файлах, без записи"));
            Assert.That(File.ReadAllText(file), Is.EqualTo(Dirty));
        }

        [Test]
        public void Fix_ApplyKeepsBomAndLineEndings()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", "");
            File.WriteAllText(file, Dirty.Replace("\n", "\r\n"), new UTF8Encoding(true));
            var config = folder.Write("style.jsonc", Config);

            var result = Cli.Run("fix", file, "--config", config, "--apply", "--hints");

            var bytes = File.ReadAllBytes(file);
            var text = File.ReadAllText(file);
            Assert.That(result.ExitCode, Is.Zero, result.Error);
            Assert.That(bytes.Take(3), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.That(text, Does.Not.Contain("///").And.Not.Contain("// return"));
            Assert.That(text, Does.Contain("return a == null || b == null;\r\n"));
            Assert.That(text.Replace("\r\n", ""), Does.Not.Contain("\n"));
            Assert.That(result.Error, Does.Contain("записано"));
        }

        [Test]
        public void Fix_JsonAndRemainingErrors()
        {
            using var folder = new TempFolder();
            var longLine = "        private int m_" + new string('x', 120) + ";";
            var source = Dirty.Replace("    private bool Check", longLine + "\n    private bool Check");
            var file = folder.Write("Sample.cs", source);
            var config = folder.Write("style.jsonc", Config);

            var result = Cli.Run("fix", file, "--config", config, "--format", "json", "--checks", "null-check");

            Assert.That(result.ExitCode, Is.EqualTo(1), "остались line-length и xml-doc");
            using var document = JsonDocument.Parse(result.Output);
            var root = document.RootElement;
            Assert.That(root.GetProperty("applied").GetBoolean(), Is.False);
            Assert.That(root.GetProperty("fixed")[0].GetProperty("check").GetString(), Is.EqualTo("null-check"));
            Assert.That(root.GetProperty("fixed")[0].GetProperty("count").GetInt32(), Is.EqualTo(2));
            Assert.That(root.GetProperty("diff").GetString(), Does.Contain("+        return a == null"));
            var remaining = root.GetProperty("remaining").GetProperty("findings").EnumerateArray()
                .Select(finding => finding.GetProperty("check").GetString());
            Assert.That(remaining, Is.EquivalentTo(new[] { "xml-doc", "line-length", "commented-code" }));
        }

        [Test]
        public void Fix_ChangedLinesOnly()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Dirty.Replace("a is null || b is null", "a.Equals(b)"));
            var config = folder.Write("style.jsonc", Config);
            Cli.InitRepository(folder.Path);
            File.WriteAllText(file, Dirty);

            var result = Cli.Run("fix", file, "--config", config, "--scope", "changed-lines", "--apply");

            var text = File.ReadAllText(file);
            Assert.That(result.ExitCode, Is.Zero, result.Error);
            Assert.That(text, Does.Contain("return a == null || b == null;"));
            Assert.That(text, Does.Contain("/// <summary>"), "строка документации не изменена");
        }

        // Готовый дифф и --scope file: только файлы диффа, все находки в них; отчет - в --out
        [Test]
        public void Fix_DiffFileScopeFileAndOut()
        {
            using var folder = new TempFolder();
            folder.Write("Sample.cs", Dirty);
            folder.Write("Other.cs", Dirty);
            var config = folder.Write("style.jsonc", Config);
            Cli.InitRepository(folder.Path);
            var diff = folder.Write("changes.diff", "+++ b/Sample.cs\n@@ -1,0 +1,1 @@\n+public class Sample\n");
            var output = Path.Combine(folder.Path, "fix.diff");

            var result = Cli.Run("fix", folder.Path, "--config", config, "--diff", diff, "--scope", "file", "--out", output);

            Assert.That(result.ExitCode, Is.Zero, result.Error);
            Assert.That(result.Output, Is.Empty);
            Assert.That(folder.Read("fix.diff"), Does.Contain("Sample.cs"));
            Assert.That(folder.Read("fix.diff"), Does.Not.Contain("Other.cs"));
            Assert.That(result.Error, Does.Contain("исправлено 3 в 1 файлах"));
        }

        [Test]
        public void Fix_UsageErrors()
        {
            using var folder = new TempFolder();
            var file = folder.Write("Sample.cs", Dirty);
            var config = folder.Write("style.jsonc", Config);

            Assert.That(Cli.Run("fix", file).ExitCode, Is.EqualTo(2));
            Assert.That(Cli.Run("fix", "--config", config).ExitCode, Is.EqualTo(2));
            Assert.That(Cli.Run("fix", file, "--config", config, "--format", "md").ExitCode, Is.EqualTo(2));
            Assert.That(Cli.Run("help", "fix").Output, Does.StartWith("csr fix <файлы | папки>"));
        }
    }
}
