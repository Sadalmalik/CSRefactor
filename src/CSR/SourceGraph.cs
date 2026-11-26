using System.Collections.Concurrent;
using CSR.Core.Config;
using CSR.Core.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR
{
    // Деревья файлов, их компиляция (SourceCompilation) и граф типов - общий вход similar (L4) и structure
    public class SourceGraph
    {
        private SourceGraph(List<SyntaxTree> trees, Compilation compilation, TypeGraph graph)
        {
            Trees = trees;
            Compilation = compilation;
            Graph = graph;
        }

        public List<SyntaxTree> Trees { get; }

        public Compilation Compilation { get; }

        public TypeGraph Graph { get; }

        public static List<SyntaxTree> Parse(IEnumerable<string> files, ProjectOptions project)
        {
            var trees = new ConcurrentBag<SyntaxTree>();
            Parallel.ForEach(files, file =>
            {
                var source = File.ReadAllText(file);
                trees.Add(CSharpSyntaxTree.ParseText(source, project.ParseOptions, SourceFileTools.GetDisplayPath(file)));
            });

            return trees.OrderBy(tree => tree.FilePath, StringComparer.Ordinal).ToList();
        }

        public static SourceGraph Build(List<SyntaxTree> trees)
        {
            var compilation = SourceCompilation.Create(trees);
            var graph = TypeGraphBuilder.Build([compilation], trees, path => path, CancellationToken.None);
            return new SourceGraph(trees, compilation, graph);
        }
    }
}
