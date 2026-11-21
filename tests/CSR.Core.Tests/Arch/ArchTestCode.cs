using CSR.Core.Arch;
using CSR.Core.Checks;
using CSR.Core.Config;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Tests.Arch
{
    public static class ArchTestCode
    {
        private static readonly ProjectOptions Project = new();

        public static List<Finding> Run(string check, string source, Action<CheckSettings>? configure = null,
            string path = "Sample.cs")
        {
            var configuration = CheckConfiguration.Create(ArchCatalog.Find(check)!, Severity.Warning, "R1");
            configure?.Invoke(configuration.Settings);
            var options = new CheckOptions();
            options.Checks.Add(configuration);
            var tree = CSharpSyntaxTree.ParseText(source, Project.ParseOptions, path);
            return SyntaxAnalyzer.Analyze(tree, options, Project);
        }

        // Тело метода в классе: строки тела начинаются с 5-й строки файла
        public static string InMethod(string body)
        {
            return $$"""
                public class Sample
                {
                    private void Method(object o, Shape shape, float speed, float angle)
                    {
                {{body}}
                    }
                }
                """;
        }
    }
}
