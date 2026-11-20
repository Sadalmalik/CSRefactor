using CSR.Core.Config;
using CSR.Core.Metrics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Tests.Metrics
{
    public static class TestCode
    {
        public const string FilePath = "Sample.cs";

        public static FileMetrics Measure(string source)
            => MetricsCalculator.Calculate(source, FilePath, new ProjectOptions().ParseOptions);

        // Метрики функции по полному имени без пространства имен: Sample.Method, Sample.Property.get
        public static MetricItem Function(string source, string name)
            => Measure(source).Functions.Single(function => function.Name == name);

        public static int Function(string source, string name, string metric)
            => Function(source, name).Values[metric];

        public static MetricItem Type(string source, string name)
            => Measure(source).Types.Single(type => type.Name == name);

        // Метод Sample.Method с заданным телом
        public static int Method(string body, string metric)
            => Function(WrapMethod(body), "Sample.Method", metric);

        public static string WrapMethod(string body)
        {
            return $$"""
                class Sample
                {
                    int Method(int a, int b, bool c, bool d, object o, int[] items, (int, int)[] pairs)
                    {
                {{body}}
                        return 0;
                    }

                    void Call(params object[] args) { }
                }
                """;
        }

        public static SyntaxNode SingleFunction(string source)
            => CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().First(Functions.IsFunction);
    }
}
