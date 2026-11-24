using CSR.Core.Text;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Tests.Text
{
    // Геометрия строк: перевод строки - свой, иначе первый в файле, иначе \n; директивы не сдвигаются
    public class TextLayoutTests
    {
        [Test]
        public void LineBreak_OwnThenFirstThenDefault()
        {
            var mixed = SourceText.From("a\nb\r\nc");

            Assert.That(TextLayout.GetLineBreak(mixed, 0), Is.EqualTo("\n"));
            Assert.That(TextLayout.GetLineBreak(mixed, 1), Is.EqualTo("\r\n"));
            Assert.That(TextLayout.GetLineBreak(mixed, 2), Is.EqualTo("\n"), "последняя строка без перевода - первый в файле");
            Assert.That(TextLayout.GetLineBreak(SourceText.From("a"), 0), Is.EqualTo("\n"));
        }

        [TestCase("        x = 1;", "        ", "    ", "    x = 1;")]
        [TestCase("    #if DEBUG", "    ", "        ", "    #if DEBUG")]
        [TestCase("   ", "    ", "", "")]
        [TestCase("  y();", "    ", "", "  y();")]
        public void Reindent(string content, string from, string to, string expected)
            => Assert.That(TextLayout.Reindent(content, from, to), Is.EqualTo(expected));

        [Test]
        public void Indent_AndDeeper()
        {
            var text = SourceText.From("class A\n{\n\tint m_Value;\n}\n");

            Assert.That(TextLayout.GetIndent(text, text.Lines[2].Start + 3), Is.EqualTo("\t"));
            Assert.That(TextLayout.Deeper("\t"), Is.EqualTo("\t\t"));
            Assert.That(TextLayout.Deeper("  "), Is.EqualTo("      "));
            Assert.That(TextLayout.IsBlank(SourceText.From("a\n  \nb"), 1), Is.True);
        }
    }
}
