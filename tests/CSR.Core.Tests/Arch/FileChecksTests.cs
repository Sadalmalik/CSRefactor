using CSR.Core.Arch;
using static CSR.Core.Tests.Arch.ArchTestCode;

namespace CSR.Core.Tests.Arch
{
    public class FileChecksTests
    {
#region partial-file

        private const string Part = """
            public partial class Mover
            {
                private int m_Block;
                private float m_Speed;

                public void A() { }
                public void B() { }
                public void C() { }
                public void D() { }
                public void E() { }
            }
            """;

        [Test]
        public void PartialFile_SubsystemPart_Found()
        {
            var finding = Run("partial-file", Part, path: "Scheme/Mover.Swim.cs").Single();
            const string message = "часть 'Mover' в файле Mover.Swim.cs: полей 2, методов 5, строк 11";
            Assert.That(finding.Message, Is.EqualTo(message));
        }

        [TestCase("Scheme/Mover.cs")]
        [TestCase("Scheme/Mover.Gizmos.cs")]
        [TestCase("Scheme/Mover.g.cs")]
        public void PartialFile_OwnFileOrAspect_NoFinding(string path)
            => Assert.That(Run("partial-file", Part, path: path), Is.Empty);

        [Test]
        public void PartialFile_SmallPartOrNotPartial_NoFinding()
        {
            var small = Run("partial-file", Part, settings => settings.Set("minMethods", 6), "Mover.Swim.cs");
            Assert.That(small, Is.Empty);
            Assert.That(Run("partial-file", Part.Replace("partial ", ""), path: "Mover.Swim.cs"), Is.Empty);
        }

#endregion


#region debug-draw

        [Test]
        public void DebugDraw_DurationAndNoFlag_Found()
        {
            const string source = """
                public class Process
                {
                    public Process()
                    {
                        Debug.DrawRay(m_Start, Vector3.up, Color.black, 15f);
                        Debug.DrawLine(m_Start, m_End);
                        if (m_Settings.DrawDebug)
                            Debug.DrawLine(m_Start, m_End, Color.red, duration: 2f);
                    }

                    private void Tick()
                    {
                        if (!m_Settings.DrawDebug)
                            return;

                        Debug.DrawLine(m_Start, m_End);
                    }

                    private void DrawPath()
                    {
                        Debug.DrawLine(m_Start, m_End, Color.red, 5f);
                    }
                }
                """;
            var findings = Run("debug-draw", source);
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 5, 6, 8 }));
            Assert.That(findings[0].Message, Is.EqualTo("Debug.DrawRay с длительностью и без флага в 'Process'"));
            Assert.That(findings[1].Message, Is.EqualTo("Debug.DrawLine без флага в 'Process'"));
            Assert.That(findings[2].Message, Is.EqualTo("Debug.DrawLine с длительностью в 'Process'"));
        }

        [Test]
        public void DebugDraw_FlagField_FoundOutsideSettings()
        {
            const string source = """
                public class Process
                {
                    public bool DrawDebug;
                    private bool m_IsDebugEnabled;
                    private bool m_IsActive;
                }

                public class ProcessSettings
                {
                    public bool DrawDebug;
                }
                """;
            var findings = Run("debug-draw", source);
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 3, 4 }));
            Assert.That(findings[0].Message, Is.EqualTo("флаг отладки 'DrawDebug' полем 'Process'"));
        }

        [Test]
        public void DebugDraw_ProjectDrawMethods()
        {
            var source = InMethod("Gizmo.Line(o, o, 2f);\nDebug.DrawLine(o, o);");
            var finding = Run("debug-draw", source, settings =>
            {
                settings.Set("drawMethods", new[] { "Gizmo.*" });
                settings.Set("durationArgument", 0);
            }).Single();
            Assert.That(finding.Message, Is.EqualTo("Gizmo.Line без флага в 'Method'"));
        }

#endregion


#region obsolete-reason

        [Test]
        public void ObsoleteReason_NoReason_Found()
        {
            const string source = """
                public class Api
                {
                    [Obsolete]
                    public void A() { }

                    [System.Obsolete("")]
                    public int B;

                    [Obsolete("Представление не должно доставать компонент")]
                    public void C() { }

                    [ObsoleteAttribute(Reason)]
                    public void D() { }
                }
                """;
            var findings = Run("obsolete-reason", source);
            Assert.That(
                findings.Select(finding => finding.Message),
                Is.EqualTo(new[] { "[Obsolete] без причины: A", "[System.Obsolete(\"\")] без причины: B" }));
        }

#endregion


#region Common

        [Test]
        public void ExcludePaths_SkipsFile()
        {
            var source = InMethod("if (shape is Circle circle) return;");
            var findings = Run(
                "type-branch",
                source,
                settings => settings.Set("excludePaths", new[] { "*/editor/*" }),
                "Game/Editor/Sample.cs");
            Assert.That(findings, Is.Empty);
        }

        [Test]
        public void Catalog_UniqueNamesAndQuestions()
        {
            Assert.That(ArchCatalog.All.Select(check => check.Name), Is.Unique);
            Assert.That(ArchCatalog.All, Has.All.Matches<ArchCheck>(check => check.Question.Length > 0));
            Assert.That(ArchCatalog.All, Has.All.Matches<ArchCheck>(check => check.Parameters[0].Name == "paths"));
            Assert.That(ArchCatalog.GetQuestion("line-length"), Is.Null);
        }

        [TestCase("m_EnterRadius", new[] { "Enter", "Radius" })]
        [TestCase("cellIndex", new[] { "cell", "Index" })]
        [TestCase("MAX_SPEED", new[] { "MAX", "SPEED" })]
        [TestCase("HTTPServer2", new[] { "HTTP", "Server", "2" })]
        public void SplitWords(string name, string[] words)
            => Assert.That(ArchSyntax.SplitWords(name), Is.EqualTo(words));

#endregion
    }
}
