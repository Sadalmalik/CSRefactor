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
                .Where(FunctionTools.IsFunction)
                .Select(function => MeasureFunction(function, text, tree.FilePath))
                .ToList();

            var fileValues = new Dictionary<string, int>
            {
                [MetricKey.Lines] = text.Lines.Count,
                [MetricKey.CodeLines] = LineCounter.CountCode(root, text),
                [MetricKey.Types] = types.Count,
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
                [MetricKey.Cyclomatic] = CyclomaticComplexity.Calculate(function),
                [MetricKey.Cognitive] = CognitiveComplexity.Calculate(function),
                [MetricKey.Lines] = LineCounter.CountPhysical(function),
                [MetricKey.CodeLines] = LineCounter.CountCode(function, text),
                [MetricKey.Nesting] = NestingDepth.Calculate(function),
                [MetricKey.Parameters] = FunctionTools.GetParameters(function)?.Parameters.Count ?? 0,
            };

            var container = GetContainer(function);
            var name = FunctionTools.GetName(function);
            var span = function.GetLocation().GetLineSpan();
            return new MetricItem
            {
                Level = MetricLevel.Function,
                Kind = FunctionTools.GetKind(function),
                Name = container.Length == 0 ? name : container + "." + name,
                Namespace = GetNamespace(function),
                Owner = GetOwnerTypes(function),
                File = path,
                Line = span.StartLinePosition.Line + 1,
                EndLine = span.EndLinePosition.Line + 1,
                Values = values,
            };
        }

        private static MetricItem MeasureType(BaseTypeDeclarationSyntax type, SourceText text, string path)
        {
            var members = MemberKind.Count(type);
            int regions = type.DescendantTrivia().Count(trivia => trivia.IsKind(SyntaxKind.RegionDirectiveTrivia));
            var values = new Dictionary<string, int>
            {
                [MetricKey.Lines] = LineCounter.CountPhysical(type),
                [MetricKey.CodeLines] = LineCounter.CountCode(type, text),
                [MetricKey.Members] = members.Values.Sum(),
                [MetricKey.Regions] = regions,
            };

            var container = GetContainer(type);
            var name = MemberKind.GetName(type);
            var span = type.GetLocation().GetLineSpan();
            return new MetricItem
            {
                Level = MetricLevel.Type,
                Kind = MemberKind.GetKind(type),
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
                    parts.Add(MemberKind.GetName(type));
                else if (FunctionTools.IsFunction(ancestor))
                    parts.Add(FunctionTools.GetName(ancestor));
            }

            parts.Reverse();
            return string.Join(".", parts);
        }

        // Только объемлющие типы: Outer.Inner
        private static string GetOwnerTypes(SyntaxNode node)
        {
            var names = node.Ancestors()
                .OfType<BaseTypeDeclarationSyntax>()
                .Select(MemberKind.GetName)
                .Reverse();

            return string.Join(".", names);
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
