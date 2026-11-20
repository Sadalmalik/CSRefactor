using CSR.Core.Metrics;

namespace CSR.Core.Tests.Metrics
{
    public class TypeAndFileMetricsTests
    {
        private const string Source = """
            using System;

            namespace Sample.Space
            {
                public class Container<T>
                {
            #region Fields

                    private const int Limit = 3;
                    private int m_A, m_B;
                    public event Action Changed;

            #endregion

                    public int Value { get; set; }

                    public Container() { }

                    public void Run() { }

                    public enum Mode
                    {
                        None,
                        First,
                        Second,
                    }

                    public delegate void Handler();
                }

                public record struct Point(int X, int Y);

                public interface IShape
                {
                    // комментарий
                    int Area();
                }
            }
            """;

        [Test]
        public void Type_MembersByKind()
        {
            MetricItem type = TestCode.Type(Source, "Container<T>");
            Assert.That(type.Kind, Is.EqualTo("class"));
            Assert.That(type.Namespace, Is.EqualTo("Sample.Space"));
            Assert.That(type.Members, Is.EquivalentTo(new Dictionary<string, int>
            {
                [TypeMembers.Constants] = 1,
                [TypeMembers.Fields] = 2,
                [TypeMembers.Events] = 1,
                [TypeMembers.Properties] = 1,
                [TypeMembers.Constructors] = 1,
                [TypeMembers.Methods] = 1,
                [TypeMembers.NestedTypes] = 2,
            }));
            Assert.That(type.Values[MetricKeys.Members], Is.EqualTo(9));
            Assert.That(type.Values[MetricKeys.Regions], Is.EqualTo(1));
        }

        [Test]
        public void Type_Lines()
        {
            MetricItem type = TestCode.Type(Source, "Container<T>");
            Assert.That(type.Line, Is.EqualTo(5));
            Assert.That(type.EndLine, Is.EqualTo(29));
            Assert.That(type.Values[MetricKeys.Lines], Is.EqualTo(25));
            Assert.That(type.Values[MetricKeys.CodeLines], Is.EqualTo(16));
        }

        [Test]
        public void Type_EnumAndRecordAndInterface()
        {
            MetricItem mode = TestCode.Type(Source, "Container<T>.Mode");
            Assert.That(mode.Kind, Is.EqualTo("enum"));
            Assert.That(mode.Members![TypeMembers.EnumValues], Is.EqualTo(3));

            Assert.That(TestCode.Type(Source, "Point").Kind, Is.EqualTo("record struct"));

            MetricItem shape = TestCode.Type(Source, "IShape");
            Assert.That(shape.Kind, Is.EqualTo("interface"));
            Assert.That(shape.Values[MetricKeys.CodeLines], Is.EqualTo(4));
        }

        [Test]
        public void File_LinesAndTypes()
        {
            MetricItem file = TestCode.Measure(Source).File;
            Assert.That(file.Name, Is.EqualTo(TestCode.FilePath));
            Assert.That(file.Values[MetricKeys.Lines], Is.EqualTo(38));
            Assert.That(file.Values[MetricKeys.CodeLines], Is.EqualTo(25));
            Assert.That(file.Values[MetricKeys.Types], Is.EqualTo(4));
        }

        [Test]
        public void File_TrailingNewline_CountsLastEmptyLine()
        {
            MetricItem file = TestCode.Measure("class A { }\r\n").File;
            Assert.That(file.Values[MetricKeys.Lines], Is.EqualTo(2));
            Assert.That(file.Values[MetricKeys.CodeLines], Is.EqualTo(1));
        }
    }
}
