using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Similar
{
    // Единицы L1 одного файла. Член - метод, конструктор, деструктор, оператор, свойство и индексатор с телом,
    // событие с add / remove, локальная функция, лямбда и анонимный метод с блоком; форма - вся декларация без
    // атрибутов и модификаторов. Фрагмент - окно из fragmentStatements подряд идущих операторов одного блока;
    // окно, равное всему телу члена, не берется (это сам член)
    public static class MemberUnitTool
    {
        public static List<SimilarUnit> Collect(SyntaxTree tree, SimilarOptions options)
        {
            var units = new List<SimilarUnit>();
            var root = tree.GetRoot();
            var owners = new Dictionary<SyntaxNode, SimilarUnit>();
            foreach (var node in root.DescendantNodes())
            {
                if (!IsUnitNode(node))
                    continue;

                var unit = CreateMember(tree, node);
                owners[node] = unit;
                if (options.Kinds.Contains(UnitKind.Member) && IsLargeEnough(unit, options))
                    units.Add(unit);
            }

            if (options.FragmentStatements < 2 || !options.Kinds.Contains(UnitKind.Fragment))
                return units;

            foreach (var block in root.DescendantNodes().OfType<BlockSyntax>())
                AddFragments(tree, block, owners, options, units);

            return units;
        }

        private static void AddFragments(
            SyntaxTree tree,
            BlockSyntax block,
            Dictionary<SyntaxNode, SimilarUnit> owners,
            SimilarOptions options,
            List<SimilarUnit> units)
        {
            int size = options.FragmentStatements;
            var statements = block.Statements;
            if (statements.Count < size || statements.Count == size && block.Parent != null && IsUnitNode(block.Parent))
                return;

            var owner = FindOwner(block, owners);
            var shapes = statements.Select(GetShape).ToList();
            for (int first = 0; first + size <= statements.Count; first++)
            {
                var window = shapes.GetRange(first, size);
                var hasher = new ShapeHasher();
                hasher.AddGroup("Window", window.Select(shape => shape.Hash));
                var fingerprints = window.SelectMany(shape => shape.Fingerprints).Concat(hasher.Fingerprints)
                    .Distinct()
                    .ToArray();
                Array.Sort(fingerprints);

                var start = statements[first];
                var end = statements[first + size - 1];
                var unit = new SimilarUnit
                {
                    Kind = UnitKind.Fragment,
                    File = tree.FilePath,
                    Member = owner?.Member ?? "",
                    StartLine = GetLine(tree, start.SpanStart),
                    EndLine = GetLine(tree, end.Span.End),
                    NodeCount = window.Sum(shape => shape.NodeCount) + 1,
                    Fingerprints = fingerprints,
                    SpanStart = start.SpanStart,
                    SpanEnd = end.Span.End,
                    BlockStart = block.SpanStart,
                    StatementIndex = first,
                    Owner = owner,
                };
                if (IsLargeEnough(unit, options))
                    units.Add(unit);
            }
        }

        internal static SimilarUnit CreateMember(SyntaxTree tree, SyntaxNode node)
        {
            var hasher = new ShapeHasher();
            hasher.Add(node);
            return new SimilarUnit
            {
                Kind = UnitKind.Member,
                File = tree.FilePath,
                Member = GetMemberName(node),
                StartLine = GetLine(tree, node.SpanStart),
                EndLine = GetLine(tree, node.Span.End),
                NodeCount = hasher.NodeCount,
                Fingerprints = hasher.GetSortedFingerprints(),
                SpanStart = node.SpanStart,
                SpanEnd = node.Span.End,
            };
        }

        private static Shape GetShape(StatementSyntax statement)
        {
            var hasher = new ShapeHasher();
            ulong hash = hasher.Add(statement);
            return new Shape(hash, hasher.Fingerprints, hasher.NodeCount);
        }

        internal static bool IsUnitNode(SyntaxNode node)
        {
            return node switch
            {
                BaseMethodDeclarationSyntax method => method.Body != null || method.ExpressionBody != null,
                PropertyDeclarationSyntax property => property.ExpressionBody != null || HasAccessorBody(property.AccessorList),
                IndexerDeclarationSyntax indexer => indexer.ExpressionBody != null || HasAccessorBody(indexer.AccessorList),
                EventDeclarationSyntax eventDeclaration => HasAccessorBody(eventDeclaration.AccessorList),
                LocalFunctionStatementSyntax function => function.Body != null || function.ExpressionBody != null,
                AnonymousFunctionExpressionSyntax function => function.Block != null,
                _ => false,
            };
        }

        private static bool HasAccessorBody(AccessorListSyntax? accessors)
            => accessors != null && accessors.Accessors.Any(accessor => accessor.Body != null || accessor.ExpressionBody != null);

        private static bool IsLargeEnough(SimilarUnit unit, SimilarOptions options)
        {
            return unit.LineCount >= options.MinLines && unit.NodeCount >= options.MinNodes
                && unit.Fingerprints.Length >= options.MinShapes;
        }

        // Ближайший член, которому принадлежит блок
        private static SimilarUnit? FindOwner(SyntaxNode node, Dictionary<SyntaxNode, SimilarUnit> owners)
        {
            foreach (var ancestor in node.Ancestors())
            {
                if (owners.TryGetValue(ancestor, out SimilarUnit? owner))
                    return owner;
            }

            return null;
        }

        // Type.Member; вложенные локальная функция и лямбда - через имя внешнего члена; тип - Outer.Type
        internal static string GetMemberName(SyntaxNode node)
        {
            if (node is BaseTypeDeclarationSyntax type)
            {
                var path = GetTypeName(type);
                return path.Length > 0 ? path + "." + type.Identifier.Text : type.Identifier.Text;
            }

            var own = GetOwnName(node);
            var outer = node.Ancestors().FirstOrDefault(ancestor => ancestor is MemberDeclarationSyntax
                and not BaseTypeDeclarationSyntax and not BaseNamespaceDeclarationSyntax);
            if (node is MemberDeclarationSyntax || outer == null)
                return GetTypeName(node) + own;

            return GetTypeName(node) + GetOwnName(outer) + (node is LocalFunctionStatementSyntax ? own : " (лямбда)");
        }

        private static string GetOwnName(SyntaxNode node)
        {
            return node switch
            {
                MethodDeclarationSyntax method => "." + method.Identifier.Text,
                ConstructorDeclarationSyntax => ".ctor",
                DestructorDeclarationSyntax => ".~dtor",
                OperatorDeclarationSyntax operatorDeclaration => ".operator " + operatorDeclaration.OperatorToken.Text,
                ConversionOperatorDeclarationSyntax conversion => ".operator " + conversion.Type,
                PropertyDeclarationSyntax property => "." + property.Identifier.Text,
                IndexerDeclarationSyntax => ".this[]",
                EventDeclarationSyntax eventDeclaration => "." + eventDeclaration.Identifier.Text,
                LocalFunctionStatementSyntax function => "." + function.Identifier.Text,
                BaseFieldDeclarationSyntax field => "." + string.Join(",", field.Declaration.Variables.Select(
                    variable => variable.Identifier.Text)),
                _ => "",
            };
        }

        private static string GetTypeName(SyntaxNode node)
        {
            var types = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().Select(type => type.Identifier.Text).Reverse();
            return string.Join(".", types);
        }

        internal static int GetLine(SyntaxTree tree, int position)
            => tree.GetLineSpan(new Microsoft.CodeAnalysis.Text.TextSpan(position, 0)).StartLinePosition.Line + 1;

        // Форма одного оператора: хеш, отпечатки, число узлов
        private class Shape
        {
            public Shape(ulong hash, IReadOnlyCollection<ulong> fingerprints, int nodeCount)
            {
                Hash = hash;
                Fingerprints = fingerprints;
                NodeCount = nodeCount;
            }

            public ulong Hash { get; }

            public IReadOnlyCollection<ulong> Fingerprints { get; }

            public int NodeCount { get; }
        }
    }
}
