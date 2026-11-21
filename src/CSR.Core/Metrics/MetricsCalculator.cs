using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Metrics
{
    // Метрики одного файла по синтаксису, без семантики и без загрузки проекта
    public static class MetricsCalculator
    {
        public static FileMetrics Calculate(SyntaxTree tree)
        {
            var root = tree.GetRoot();
            var text = tree.GetText();

            var types = root.DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Select(type => MeasureType(type, text, tree.FilePath))
                .ToList();

            var functions = root.DescendantNodes()
                .Where(Functions.IsFunction)
                .Select(function => MeasureFunction(function, text, tree.FilePath))
                .ToList();

            var fileValues = new Dictionary<string, int>
            {
                [MetricKeys.Lines] = text.Lines.Count,
                [MetricKeys.CodeLines] = LineCounter.CountCode(root, text),
                [MetricKeys.Types] = types.Count,
            };

            var file = new MetricItem
            {
                Level = MetricLevel.File,
                Kind = "file",
                Name = tree.FilePath,
                Namespace = "",
                File = tree.FilePath,
                Line = 1,
                EndLine = text.Lines.Count,
                Values = fileValues,
            };

            return new FileMetrics { File = file, Types = types, Functions = functions };
        }

        public static FileMetrics Calculate(string source, string path, CSharpParseOptions? options = null)
            => Calculate(CSharpSyntaxTree.ParseText(source, options, path));

        private static MetricItem MeasureFunction(SyntaxNode function, SourceText text, string path)
        {
            var values = new Dictionary<string, int>
            {
                [MetricKeys.Cyclomatic] = CyclomaticComplexity.Calculate(function),
                [MetricKeys.Cognitive] = CognitiveComplexity.Calculate(function),
                [MetricKeys.Lines] = LineCounter.CountPhysical(function),
                [MetricKeys.CodeLines] = LineCounter.CountCode(function, text),
                [MetricKeys.Nesting] = NestingDepth.Calculate(function),
                [MetricKeys.Parameters] = Functions.GetParameters(function)?.Parameters.Count ?? 0,
            };

            var container = GetContainer(function);
            var name = Functions.GetName(function);
            var span = function.GetLocation().GetLineSpan();
            return new MetricItem
            {
                Level = MetricLevel.Function,
                Kind = Functions.GetKind(function),
                Name = container.Length == 0 ? name : container + "." + name,
                Namespace = GetNamespace(function),
                File = path,
                Line = span.StartLinePosition.Line + 1,
                EndLine = span.EndLinePosition.Line + 1,
                Values = values,
            };
        }

        private static MetricItem MeasureType(BaseTypeDeclarationSyntax type, SourceText text, string path)
        {
            var members = TypeMembers.Count(type);
            int regions = type.DescendantTrivia().Count(trivia => trivia.IsKind(SyntaxKind.RegionDirectiveTrivia));
            var values = new Dictionary<string, int>
            {
                [MetricKeys.Lines] = LineCounter.CountPhysical(type),
                [MetricKeys.CodeLines] = LineCounter.CountCode(type, text),
                [MetricKeys.Members] = members.Values.Sum(),
                [MetricKeys.Regions] = regions,
            };

            var container = GetContainer(type);
            var name = TypeMembers.GetName(type);
            var span = type.GetLocation().GetLineSpan();
            return new MetricItem
            {
                Level = MetricLevel.Type,
                Kind = TypeMembers.GetKind(type),
                Name = container.Length == 0 ? name : container + "." + name,
                Namespace = GetNamespace(type),
                File = path,
                Line = span.StartLinePosition.Line + 1,
                EndLine = span.EndLinePosition.Line + 1,
                Values = values,
                Members = members,
            };
        }

        // Объемлющие типы и функции: Outer.Inner, Outer.Method для локальной функции
        private static string GetContainer(SyntaxNode node)
        {
            var parts = new List<string>();
            foreach (var ancestor in node.Ancestors())
            {
                if (ancestor is BaseTypeDeclarationSyntax type)
                    parts.Add(TypeMembers.GetName(type));
                else if (Functions.IsFunction(ancestor))
                    parts.Add(Functions.GetName(ancestor));
            }

            parts.Reverse();
            return string.Join(".", parts);
        }

        private static string GetNamespace(SyntaxNode node)
        {
            var names = node.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Select(declaration => declaration.Name.ToString())
                .Reverse();

            return string.Join(".", names);
        }
    }
}
