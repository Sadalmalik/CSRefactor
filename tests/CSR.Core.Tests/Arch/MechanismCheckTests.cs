using CSR.Core.Config;
using static CSR.Core.Tests.Arch.ArchTestCode;

namespace CSR.Core.Tests.Arch
{
    public class MechanismCheckTests
    {
        private static readonly ProjectOptions Project = new()
        {
            Mechanisms =
            [
                new ProjectMechanism
                {
                    Name = "pool",
                    Use = "ObjectPool<T>",
                    Types = ["*Pool"],
                    Fields = ["Stack<*>", "Queue<*>"],
                    Methods = ["Rent*", "Return*"],
                    ExcludePaths = ["*/Pooling/*"],
                },
                new ProjectMechanism
                {
                    Name = "logger",
                    Use = "Log.Error",
                    Calls = ["Debug.Log*"],
                    Creates = ["Timer"],
                },
            ],
        };

        [Test]
        public void Mechanism_FieldAndMethods_Found()
        {
            const string source = """
                public class PathService
                {
                    private readonly Stack<Filter> m_Filters = new();

                    public Filter RentFilter() => m_Filters.Pop();

                    public void ReturnFilter(Filter filter) => m_Filters.Push(filter);
                }
                """;
            var finding = Run("project-mechanism", source, Project).Single();
            Assert.That(finding.Line, Is.EqualTo(1));
            const string message = "свой 'pool' в 'PathService' (поле Stack<Filter>, методы RentFilter, ReturnFilter); "
                + "в проекте: ObjectPool<T>";
            Assert.That(finding.Message, Is.EqualTo(message));
        }

        [Test]
        public void Mechanism_TypeName_Found()
        {
            var finding = Run("project-mechanism", "public class BulletPool { }", Project).Single();
            Assert.That(finding.Message, Does.StartWith("свой 'pool' в 'BulletPool' (имя)"));
        }

        [Test]
        public void Mechanism_PartialMatchOrExcludedPath_NoFinding()
        {
            const string source = """
                public class Cache
                {
                    private readonly Stack<Filter> m_Filters = new();

                    public Filter RentFilter() => m_Filters.Pop();
                }

                public class Router
                {
                    public Filter RentFilter() => null;
                    public void ReturnFilter(Filter filter) { }
                }
                """;
            Assert.That(Run("project-mechanism", source, Project), Is.Empty);
            Assert.That(
                Run("project-mechanism", "class BulletPool { }", Project, path: "Game/Pooling/BulletPool.cs"),
                Is.Empty);
        }

        [Test]
        public void Mechanism_CallsAndCreates_Found()
        {
            const string body = """
                Debug.LogError("fail");
                Log.Error("fail");
                var timer = new Timer(1000);
                """;
            var findings = Run("project-mechanism", InMethod(body), Project);
            Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[] { 5, 7 }));
            Assert.That(findings[0].Message, Is.EqualTo("'Debug.LogError' в обход 'logger'; в проекте: Log.Error"));
            Assert.That(findings[1].Message, Does.StartWith("'Timer' в обход 'logger'"));
        }

        [Test]
        public void Mechanism_NoMechanisms_NoFinding()
            => Assert.That(Run("project-mechanism", "public class BulletPool { }"), Is.Empty);
    }
}
