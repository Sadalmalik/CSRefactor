using static CSR.Core.Tests.Style.StyleTestCode;

namespace CSR.Core.Tests.Style
{
    public class StructureChecksTests
    {
#region explicit-private

        [Test]
        public void ExplicitPrivate_FieldAndMethod_InsertsPrivate()
        {
            const string source = """
                public class Sample
                {
                    [SerializeField]
                    float m_Speed;
                    static int s_Count;
                    bool CheckHazard(int point) => point > 0;
                    public int Value { get; set; }
                }
                """;
            var findings = Run("explicit-private", source);
            Assert.That(findings.Select(finding => finding.Replacement![0].Trim()), Is.EqualTo(new[]
            {
                "private float m_Speed;",
                "private static int s_Count;",
                "private bool CheckHazard(int point) => point > 0;",
            }));
            Assert.That(findings.Select(finding => (finding.Line, finding.EndLine)), Is.EqualTo(new[]
            {
                (4, 4),
                (5, 5),
                (6, 6),
            }), "строка с атрибутом не входит в находку: замена - одна строка");
        }

        [Test]
        public void ExplicitPrivate_MultilineMethod_ReplacesSignatureLineOnly()
        {
            const string source = """
                public class Sample
                {
                    static void Add(int[] items, int item)
                    {
                        items[0] = item;
                    }
                }
                """;
            var finding = Run("explicit-private", source).Single();
            Assert.That((finding.Line, finding.EndLine), Is.EqualTo((3, 3)));
            var expected = "    private static void Add(int[] items, int item)";
            Assert.That(finding.Replacement, Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void ExplicitPrivate_Exclusions_NoFinding()
        {
            const string source = """
                public interface IShape
                {
                    int Area();
                }

                public partial class Sample : IShape
                {
                    static Sample() { }
                    ~Sample() { }
                    int IShape.Area() => 0;
                    partial void OnLoaded();
                    private int m_Value;
                }
                """;
            Assert.That(Run("explicit-private", source), Is.Empty);
        }

        [Test]
        public void ExplicitPrivate_PreferImplicit_RemovesPrivate()
        {
            const string source = """
                public class Sample
                {
                    private int m_Value;
                }
                """;
            var findings = Run("explicit-private", source, settings => settings.Set("prefer", "implicit"));
            Assert.That(findings.Single().Replacement, Is.EqualTo(new[] { "    int m_Value;" }));
        }

#endregion


#region forbidden-modifier

        [Test]
        public void ForbiddenModifier_AllForms_Found()
        {
            const string source = """
                internal sealed class Splitter
                {
                    private protected int m_A;
                    internal protected int m_B;
                    protected int m_C;
                }
                """;
            var messages = Run("forbidden-modifier", source).Select(finding => finding.Message);
            Assert.That(messages, Is.EqualTo(new[]
            {
                "модификатор 'sealed'",
                "модификатор 'private protected'",
                "модификатор 'protected internal'",
            }));
        }

#endregion


#region custom-delegate

        [Test]
        public void CustomDelegate_Found()
        {
            const string source = """
                public delegate void Handler(int value);

                public class Sample
                {
                    public event System.Action<int> OnChanged;
                }
                """;
            Assert.That(Run("custom-delegate", source).Single().Line, Is.EqualTo(1));
        }

#endregion
    }
}
