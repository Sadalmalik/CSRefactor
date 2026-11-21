using CSR.Core.Checks;
using static CSR.Core.Tests.Arch.SolutionChecksTests;

namespace CSR.Core.Tests.Arch
{
    public class ClassCohesionCheckTests
    {
        private const string Handler = """
            public class Handler
            {
                private int m_Step;
                private float m_Height;

                public Handler()
                {
                    m_Step = 0;
                    m_Height = 0;
                }

                private void Awake()
                {
                    m_Step = 1;
                    m_Height = 1;
                }

            #region Path

                public void Move() => Advance();

                private void Advance() => m_Step++;

                private bool CanMove() => m_Step < 10;

                public int Step => m_Step;

            #endregion

            #region Wall

                public void Climb() => Lift();

                private void Lift() => m_Height += LINK;

                private bool CanClimb() => m_Height < 10;

                public float Height => m_Height;

            #endregion

                private static int Zero() => 0;
            }

            public class Mover
            {
                public void Run(Handler handler) => handler.Move();
            }

            public class Climber
            {
                public void Run(Handler handler) => handler.Climb();
            }
            """;

        private const string WallPart = """
            public partial class Chain
            {
            #region Wall

                public void Climb() => Lift();

                private void Lift() => m_Height += 1;

                private bool CanClimb() => m_Height < 10;

                public float Height => m_Height;

            #endregion
            }
            """;

        private const string PathPart = """
            public partial class Chain
            {
                private int m_Step;
                private float m_Height;

                public void Move() => Advance();

                private void Advance() => m_Step++;

                private bool CanMove() => m_Step < 10;

                public int Step => m_Step;
            }
            """;

        private static List<Finding> Run(params string[] files)
        {
            return Analyze([Compile("Game", files)], "class-cohesion", settings =>
            {
                settings.Set("minMethods", 4);
            });
        }

        [Test]
        public void TwoParts_Found()
        {
            var finding = Run("Handler.cs", Handler.Replace("LINK", "1")).Single();
            Assert.That(finding.Line, Is.EqualTo(1));
            const string message = "'Handler': несвязанных частей 2 (методов 7): "
                + "1) Move, Advance, CanMove, Step | поля m_Step | регион Path | потребители Mover; "
                + "2) Climb, Lift, CanClimb, Height | поля m_Height | регион Wall | потребители Climber";
            Assert.That(finding.Message, Is.EqualTo(message));
        }

        [Test]
        public void SharedField_OnePart_NoFinding()
            => Assert.That(Run("Handler.cs", Handler.Replace("LINK", "m_Step")), Is.Empty);

        [Test]
        public void SmallClass_NoFinding()
        {
            var findings = Analyze(
                [Compile("Game", "Handler.cs", Handler.Replace("LINK", "1"))],
                "class-cohesion");
            Assert.That(findings, Is.Empty);
        }

        [Test]
        public void Partial_FilesInMessage()
        {
            var finding = Run("Chain.cs", PathPart, "Chain.Wall.cs", WallPart).Single();
            Assert.That(finding.File, Is.EqualTo("Chain.cs"));
            Assert.That(finding.Message, Does.Contain("1) Move, Advance, CanMove, Step | поля m_Step | Chain.cs;"));
            const string wall = "2) Climb, Lift, CanClimb, Height | поля m_Height | регион Wall | Chain.Wall.cs";
            Assert.That(finding.Message, Does.EndWith(wall));
        }
    }
}
