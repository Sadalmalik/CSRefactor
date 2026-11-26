namespace CSR.Tests
{
    public class CommandLineTests
    {
        private static readonly HashSet<string> Options = ["--config", "--out"];
        private static readonly HashSet<string> Flags = ["--all"];

        [Test]
        public void Parse_PositionalOptionsAndFlags()
        {
            var commandLine = CommandLine.Parse(
                ["a", "--config", "x.jsonc", "b", "--all", "--config", "y.jsonc"],
                Options,
                Flags);

            Assert.That(commandLine.Positional, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(commandLine.HasFlag("--all"), Is.True);
            Assert.That(commandLine.GetOptions("--config"), Is.EqualTo(new[] { "x.jsonc", "y.jsonc" }));
            Assert.That(commandLine.GetOption("--out"), Is.Null);
            Assert.That(commandLine.GetOptions("--out"), Is.Empty);
        }

        [Test]
        public void GetOption_Repeated_Throws()
        {
            var commandLine = CommandLine.Parse(["--out", "a", "--out", "b"], Options, Flags);
            Assert.Throws<UsageException>(() => commandLine.GetOption("--out"));
        }

        [TestCase("--unknown")]
        [TestCase("--out")]
        public void Parse_UnknownOrWithoutValue_Throws(string arg)
            => Assert.Throws<UsageException>(() => CommandLine.Parse([arg], Options, Flags));
    }

    public class SourceFilesTests
    {
        [Test]
        public void Collect_SkipsBinObjAndHiddenFolders()
        {
            using var folder = new TempFolder();
            var kept = folder.Write("src/A.cs", "class A { }");
            var single = folder.Write("B.cs", "class B { }");
            folder.Write("src/bin/Skip.cs", "");
            folder.Write("src/obj/Skip.cs", "");
            folder.Write("src/.hidden/Skip.cs", "");
            folder.Write("src/Notes.txt", "");

            var files = SourceFileTools.Collect([System.IO.Path.Combine(folder.Path, "src"), single]);
            Assert.That(files, Is.EquivalentTo(new[] { kept, single }));
        }

        [Test]
        public void Collect_MissingPath_Throws()
            => Assert.Throws<UsageException>(() => SourceFileTools.Collect(["no-such-folder-csr"]));

        [Test]
        public void DisplayPath_InsideCurrentFolderRelative_OutsideFull()
        {
            var inside = Path.Combine(Environment.CurrentDirectory, "sub", "A.cs");
            Assert.That(SourceFileTools.GetDisplayPath(inside), Is.EqualTo("sub/A.cs"));

            using var folder = new TempFolder();
            var outside = Path.Combine(folder.Path, "A.cs");
            Assert.That(SourceFileTools.GetDisplayPath(outside), Is.EqualTo(outside.Replace('\\', '/')));
        }
    }

    public class OutputTests
    {
        [Test]
        public void Write_ToFileWithoutBom()
        {
            using var folder = new TempFolder();
            var path = Path.Combine(folder.Path, "out.txt");
            Output.Write("текст", path);

            var bytes = File.ReadAllBytes(path);
            Assert.That(bytes[0], Is.Not.EqualTo(0xEF));
            Assert.That(File.ReadAllText(path), Is.EqualTo("текст"));
        }
    }
}
