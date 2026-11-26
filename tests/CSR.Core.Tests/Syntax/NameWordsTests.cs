using CSR.Core.Syntax;

namespace CSR.Core.Tests.Syntax
{
    public class NameWordsTests
    {
        [TestCase("IsUIVisible", "Is UI Visible")]
        [TestCase("m_isOn", "m is On")]
        [TestCase("MAX_POOL_SIZE", "MAX POOL SIZE")]
        [TestCase("Vector3Pool", "Vector3 Pool")]
        public void Split(string name, string expected)
            => Assert.That(string.Join(" ", NameWords.Split(name)), Is.EqualTo(expected));

        [TestCase("m_EnterRadius", new[] { "Enter", "Radius" })]
        [TestCase("cellIndex", new[] { "cell", "Index" })]
        [TestCase("MAX_SPEED", new[] { "MAX", "SPEED" })]
        [TestCase("HTTPServer2", new[] { "HTTP", "Server2" })]
        [TestCase("Vector3Pool", new[] { "Vector3", "Pool" })]
        public void GetWords(string name, string[] words)
            => Assert.That(NameWords.GetWords(name), Is.EqualTo(words));

        [TestCase("MAX_POOL_SIZE", "MaxPoolSize")]
        [TestCase("m_count", "Count")]
        [TestCase("maxSize", "MaxSize")]
        [TestCase("game.Items", "Game.Items")]
        [TestCase("@class", "Class")]
        public void ToPascal(string name, string expected)
            => Assert.That(NameWords.ToPascal(name), Is.EqualTo(expected));
    }
}
