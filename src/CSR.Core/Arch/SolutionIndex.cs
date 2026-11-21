using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch
{
    // Семантический индекс выбранных компиляций для признаков по решению: объявленные типы и ссылки на символы
    // (чтение, запись, вызов). Строится один раз на прогон, при первом обращении. Символы из разных проектов
    // сводятся по ключу (DocumentationCommentId)
    public class SolutionIndex
    {
        private readonly IReadOnlyList<Compilation> m_Compilations;
        private readonly CancellationToken m_Cancellation;
        private readonly Dictionary<string, List<SymbolReference>> m_References = new();
        private readonly List<DeclaredType> m_Types = new();
        private bool m_IsBuilt;

        public SolutionIndex(IReadOnlyList<Compilation> compilations, CancellationToken cancellation)
        {
            m_Compilations = compilations;
            m_Cancellation = cancellation;
        }

        // Типы, объявленные в деревьях выбранных проектов (partial - один раз), с моделью первого объявления
        public IReadOnlyList<DeclaredType> Types
        {
            get
            {
                Build();
                return m_Types;
            }
        }

        public IReadOnlyList<SymbolReference> GetReferences(ISymbol symbol)
        {
            Build();
            return m_References.TryGetValue(GetKey(symbol), out List<SymbolReference>? references)
                ? references
                : Array.Empty<SymbolReference>();
        }

        // Ключ символа, общий для его копий в разных компиляциях
        public static string GetKey(ISymbol symbol)
        {
            var definition = symbol is IMethodSymbol { ReducedFrom: { } reduced } ? reduced : symbol;
            definition = definition.OriginalDefinition;
            if (definition is IParameterSymbol parameter)
                return GetKey(parameter.ContainingSymbol) + "#" + parameter.Ordinal;

            return definition.GetDocumentationCommentId() ?? definition.ToDisplayString();
        }

        private void Build()
        {
            if (m_IsBuilt)
                return;

            m_IsBuilt = true;
            var seenTypes = new HashSet<string>();
            var trees = new HashSet<SyntaxTree>(ArchSolutionAnalyzer.GetTrees(m_Compilations));
            foreach (var compilation in m_Compilations)
            {
                foreach (var tree in compilation.SyntaxTrees.Where(trees.Remove))
                {
                    m_Cancellation.ThrowIfCancellationRequested();
                    var model = compilation.GetSemanticModel(tree);
                    IndexTree(model, tree.GetRoot(m_Cancellation), seenTypes);
                }
            }
        }

        private void IndexTree(SemanticModel model, SyntaxNode root, HashSet<string> seenTypes)
        {
            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case BaseTypeDeclarationSyntax declaration:
                        AddType(model, declaration, seenTypes);
                        break;
                    case SimpleNameSyntax name:
                        AddReference(model, name, model.GetSymbolInfo(name, m_Cancellation));
                        break;
                    case BaseObjectCreationExpressionSyntax creation:
                        AddReference(model, creation, model.GetSymbolInfo(creation, m_Cancellation));
                        break;
                }
            }
        }

        private void AddType(SemanticModel model, BaseTypeDeclarationSyntax declaration, HashSet<string> seenTypes)
        {
            if (model.GetDeclaredSymbol(declaration, m_Cancellation) is not INamedTypeSymbol type)
                return;

            if (seenTypes.Add(GetKey(type)))
                m_Types.Add(new DeclaredType(type, model));
        }

        private void AddReference(SemanticModel model, SyntaxNode node, SymbolInfo info)
        {
            var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
            if (symbol == null || symbol.Kind is SymbolKind.Local or SymbolKind.Namespace or SymbolKind.Label
                or SymbolKind.RangeVariable or SymbolKind.TypeParameter)
                return;

            var key = GetKey(symbol);
            if (!m_References.TryGetValue(key, out List<SymbolReference>? references))
            {
                references = new List<SymbolReference>();
                m_References[key] = references;
            }

            references.Add(new SymbolReference(node, model, GetAccess(node)));
        }

        // Чтение, запись или то и другое: x = 1 - запись; x += 1, x++, ref x - оба; out x - запись
        private static ReferenceAccess GetAccess(SyntaxNode node)
        {
            SyntaxNode expression = node;
            if (node.Parent is MemberAccessExpressionSyntax access && access.Name == node)
                expression = access;
            else if (node.Parent is MemberBindingExpressionSyntax binding)
                expression = binding;

            switch (expression.Parent)
            {
                case AssignmentExpressionSyntax assignment when assignment.Left == expression:
                    return assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                        ? ReferenceAccess.Write
                        : ReferenceAccess.ReadWrite;
                case PostfixUnaryExpressionSyntax:
                    return ReferenceAccess.ReadWrite;
                case PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.PreIncrementExpression)
                    || prefix.IsKind(SyntaxKind.PreDecrementExpression):
                    return ReferenceAccess.ReadWrite;
                case ArgumentSyntax argument when argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword):
                    return ReferenceAccess.Write;
                case ArgumentSyntax argument when argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword):
                    return ReferenceAccess.ReadWrite;
                default:
                    return ReferenceAccess.Read;
            }
        }
    }

    public enum ReferenceAccess
    {
        Read,
        Write,
        ReadWrite,
    }

    // Место ссылки на символ: узел имени или new, модель его дерева и вид доступа
    public class SymbolReference
    {
        public SymbolReference(SyntaxNode node, SemanticModel model, ReferenceAccess access)
        {
            Node = node;
            Model = model;
            Access = access;
        }

        public SyntaxNode Node { get; }

        public SemanticModel Model { get; }

        public ReferenceAccess Access { get; }

        public bool IsRead
            => Access != ReferenceAccess.Write;

        // Вызов: имя - выражение вызова (M(), x.M(), x?.M()) или new
        public bool IsInvocation
        {
            get
            {
                if (Node is BaseObjectCreationExpressionSyntax)
                    return true;

                SyntaxNode expression = Node;
                if (Node.Parent is MemberAccessExpressionSyntax access && access.Name == Node)
                    expression = access;
                else if (Node.Parent is MemberBindingExpressionSyntax binding)
                    expression = binding;

                var invocation = expression.Parent as InvocationExpressionSyntax;
                return invocation != null && invocation.Expression == expression;
            }
        }

        // Вызов, к которому относится ссылка; null - не вызов
        public ArgumentListSyntax? GetArguments()
        {
            if (Node is BaseObjectCreationExpressionSyntax creation)
                return creation.ArgumentList;

            return IsInvocation ? Node.FirstAncestorOrSelf<InvocationExpressionSyntax>()?.ArgumentList : null;
        }
    }

    // Тип из исходников выбранных проектов
    public class DeclaredType
    {
        public DeclaredType(INamedTypeSymbol symbol, SemanticModel model)
        {
            Symbol = symbol;
            Model = model;
        }

        public INamedTypeSymbol Symbol { get; }

        public SemanticModel Model { get; }
    }
}
