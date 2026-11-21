using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSR.Core.Arch.Checks
{
    // bool-флаг варианта в общем коде: не private bool-свойство или поле, по которому ветвится код других типов
    // (условие if, while, ?:, when, операнд &&, ||, !) не меньше чем в minFiles файлах. Находка - у объявления,
    // со списком файлов. Признак по решению: считает места во всех выбранных проектах
    public class VariantFlagCheck : SolutionCheck
    {
        private static readonly CheckParameter[] AllParameters =
        [
            CheckParameter.Int("minFiles", 3, "в скольких файлах вне своего типа флаг ветвит код - находка"),
            CheckParameter.Int("minUses", 0, "сколько мест ветвления всего - находка"),
            CheckParameter.Strings("names", "маски имен флагов (без учета регистра)", "*"),
            CheckParameter.Strings("ignoreNames", "маски имен флагов, которые не ищутся (без учета регистра)"),
        ];

        public override string Name
            => "variant-flag";

        public override string Description
            => "bool-флаг варианта в общем коде";

        public override string Question
            => "Флаг заменяет вариант поведения? Тогда - метод базы или интерфейса, который вариант реализует сам";

        protected override IReadOnlyList<CheckParameter> OwnParameters
            => AllParameters;

        public override void AnalyzeSolution(SolutionCheckContext context)
        {
            var names = NameMask.Get(context.Settings.GetStrings("names"), true);
            var ignored = NameMask.Get(context.Settings.GetStrings("ignoreNames"), true);
            var flags = new Dictionary<string, FlagUsage>();
            var trees = new HashSet<SyntaxTree>(ArchSolutionAnalyzer.GetTrees(context.Compilations));
            foreach (var compilation in context.Compilations)
            {
                foreach (var tree in compilation.SyntaxTrees.Where(trees.Remove))
                {
                    context.Cancellation.ThrowIfCancellationRequested();
                    CollectTree(compilation, tree, flags, names, ignored);
                }
            }

            var minFiles = context.Settings.GetInt("minFiles");
            var minUses = context.Settings.GetInt("minUses");
            foreach (var flag in flags.Values.OrderBy(usage => usage.Key, StringComparer.Ordinal))
            {
                if (flag.Files.Count < minFiles || flag.Uses < minUses)
                    continue;

                var files = flag.Files
                    .Select(file => System.IO.Path.GetFileName(context.GetDisplayPath(file)))
                    .OrderBy(file => file, StringComparer.Ordinal)
                    .ToList();
                var message = $"флаг '{flag.Symbol.ContainingType.Name}.{flag.Symbol.Name}' ветвит код других типов: "
                    + $"мест {flag.Uses} в {flag.Files.Count} файлах: {ArchSyntax.JoinNames(files)}";
                context.Report(flag.Symbol.Locations.First(location => location.IsInSource), message);
            }
        }

        private static void CollectTree(
            Compilation compilation,
            SyntaxTree tree,
            Dictionary<string, FlagUsage> flags,
            NameMask names,
            NameMask ignored)
        {
            SemanticModel? model = null;
            foreach (var name in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var text = name.Identifier.Text;
                if (!names.IsMatch(text) || ignored.IsMatch(text) || !IsBranchCondition(name))
                    continue;

                model ??= compilation.GetSemanticModel(tree);
                var symbol = model.GetSymbolInfo(name).Symbol;
                if (symbol == null || !IsFlag(symbol))
                    continue;

                var flag = symbol.OriginalDefinition;
                var user = model.GetEnclosingSymbol(name.SpanStart)?.ContainingType;
                if (user != null && IsSameType(user, flag.ContainingType))
                    continue;

                var key = flag.GetDocumentationCommentId() ?? flag.ToDisplayString();
                if (!flags.TryGetValue(key, out FlagUsage? usage))
                {
                    usage = new FlagUsage(key, flag);
                    flags[key] = usage;
                }

                usage.Add(tree.FilePath);
            }
        }

        // Не private bool-свойство или поле из исходников (не константа)
        private static bool IsFlag(ISymbol symbol)
        {
            var type = symbol switch
            {
                IPropertySymbol property => property.Type,
                IFieldSymbol { IsConst: false } field => field.Type,
                _ => null,
            };

            if (type == null || type.SpecialType != SpecialType.System_Boolean)
                return false;

            if (symbol.DeclaredAccessibility == Accessibility.Private)
                return false;

            return symbol.Locations.Any(location => location.IsInSource);
        }

        private static bool IsSameType(INamedTypeSymbol user, INamedTypeSymbol owner)
        {
            for (var type = user; type != null; type = type.ContainingType)
            {
                if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, owner.OriginalDefinition))
                    return true;
            }

            return false;
        }

        // Имя - сам флаг (x.Flag, x?.Flag, Flag), и выражение с ним - условие ветвления
        private static bool IsBranchCondition(IdentifierNameSyntax name)
        {
            SyntaxNode expression = name;
            if (name.Parent is MemberAccessExpressionSyntax access && access.Name == name)
                expression = access;
            else if (name.Parent is MemberBindingExpressionSyntax binding)
                expression = binding.Parent is ConditionalAccessExpressionSyntax conditional ? conditional : binding;

            while (expression.Parent is ParenthesizedExpressionSyntax or PrefixUnaryExpressionSyntax
                or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalAndExpression }
                or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression })
            {
                var parent = expression.Parent;
                if (parent is PrefixUnaryExpressionSyntax unary && !unary.IsKind(SyntaxKind.LogicalNotExpression))
                    return false;

                expression = parent;
            }

            return expression.Parent switch
            {
                IfStatementSyntax statement => statement.Condition == expression,
                WhileStatementSyntax loop => loop.Condition == expression,
                DoStatementSyntax loop => loop.Condition == expression,
                ConditionalExpressionSyntax conditional => conditional.Condition == expression,
                WhenClauseSyntax => true,
                _ => false,
            };
        }

        // Места ветвления по одному флагу: файлы и число мест
        private class FlagUsage
        {
            public FlagUsage(string key, ISymbol symbol)
            {
                Key = key;
                Symbol = symbol;
            }

            public string Key { get; }

            public ISymbol Symbol { get; }

            public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

            public int Uses { get; private set; }

            public void Add(string file)
            {
                Files.Add(file);
                Uses++;
            }
        }
    }
}
