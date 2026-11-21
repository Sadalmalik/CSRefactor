using CSR.Core.Config;
using CSR.Core.Style;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Tests.Style
{
    public static class StyleTestCode
    {
        private static readonly ProjectOptions DefaultProject = new()
        {
            LoggerMethods = ["Debug.Log", "Debug.LogError", "*.Logger.Error"],
        };

        public static List<Finding> Run(string check, string source, Action<CheckSettings>? configure = null)
        {
            var configuration = StyleCatalog.Configure(StyleCatalog.Find(check)!, Severity.Error, "R1");
            configure?.Invoke(configuration.Settings);
            var options = new StyleOptions();
            options.Checks.Add(configuration);
            var tree = CSharpSyntaxTree.ParseText(source, DefaultProject.ParseOptions, "Sample.cs");
            return StyleAnalyzer.Analyze(tree, options, DefaultProject);
        }

        // Тело метода в классе: строки тела начинаются с 7-й строки файла, отступ тела - как в body
        public static string InMethod(string body)
        {
            return $$"""
                using System;

                public class Sample
                {
                    private void Method(object o, int a, bool c)
                    {
                {{body}}
                    }
                }
                """;
        }
    }
}
