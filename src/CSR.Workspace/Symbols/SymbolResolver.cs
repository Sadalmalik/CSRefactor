using CSR.Workspace.Loading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Symbols
{
    // Символ по запросу: объявления в коде решения по полному имени, затем типы и члены из метаданных
    // (сборки Unity и т. п.), или символ в позиции файла
    public static class SymbolResolver
    {
        // allowOverloads: несколько перегрузок метода - все (refs); иначе неоднозначность - ошибка (rename)
        public static async Task<IReadOnlyList<ISymbol>> ResolveAsync(LoadedSolution loaded, SymbolQuery query,
            string baseDirectory, bool allowOverloads, CancellationToken cancellation = default)
        {
            if (query.IsPosition)
            {
                var symbol = await ResolvePositionAsync(loaded, query, baseDirectory, cancellation);
                return [symbol];
            }

            var symbols = await FindSourceAsync(loaded.Current, query, cancellation);
            if (symbols.Count == 0)
                symbols = await FindMetadataAsync(loaded.Current, query, cancellation);

            if (symbols.Count == 0)
                throw new SymbolException($"символ '{query.Text}' не найден");

            if (symbols.Count == 1 || allowOverloads && symbols.All(symbol => symbol is IMethodSymbol))
                return symbols;

            var candidates = string.Join("\n", symbols.Select(symbol => "  " + SymbolText.Query(symbol)));
            throw new SymbolException($"символ '{query.Text}' неоднозначен, уточните параметры:\n{candidates}");
        }

#region Name

        private static async Task<List<ISymbol>> FindSourceAsync(Solution solution, SymbolQuery query,
            CancellationToken cancellation)
        {
            var last = query.LastSegment;
            var declared = await SymbolFinder.FindSourceDeclarationsAsync(solution, name => name == last,
                SymbolFilter.TypeAndMember, cancellation);
            var symbols = declared
                .Where(symbol => SymbolText.Qualified(symbol) == query.Name && MatchesParameters(symbol, query))
                .ToList();
            return Distinct(symbols);
        }

        private static async Task<List<ISymbol>> FindMetadataAsync(Solution solution, SymbolQuery query,
            CancellationToken cancellation)
        {
            var name = query.Name!;
            var split = name.LastIndexOf('.');
            var projects = solution.Projects
                .Where(project => project.Language == LanguageNames.CSharp)
                .OrderBy(project => project.DocumentIds.Count);
            foreach (var project in projects)
            {
                var compilation = await project.GetCompilationAsync(cancellation);
                if (compilation == null)
                    continue;

                var type = compilation.GetTypeByMetadataName(name);
                if (type != null)
                    return [type];

                if (split < 0)
                    continue;

                var container = compilation.GetTypeByMetadataName(name.Substring(0, split));
                var members = container?.GetMembers(query.LastSegment)
                    .Where(member => MatchesParameters(member, query))
                    .ToList();
                if (members is { Count: > 0 })
                    return members;
            }

            return [];
        }

        private static bool MatchesParameters(ISymbol symbol, SymbolQuery query)
        {
            if (query.Parameters == null)
                return true;

            if (symbol is not IMethodSymbol method || method.Parameters.Length != query.Parameters.Count)
                return false;

            for (var i = 0; i < method.Parameters.Length; i++)
            {
                var actual = SymbolText.ParameterType(method.Parameters[i]).Replace(" ", "");
                if (actual != query.Parameters[i])
                    return false;
            }

            return true;
        }

        // Один символ может попасть в несколько проектов (связанные файлы): по месту объявления
        private static List<ISymbol> Distinct(List<ISymbol> symbols)
        {
            return symbols
                .GroupBy(GetDeclarationKey)
                .Select(group => group.First())
                .ToList();
        }

        private static string GetDeclarationKey(ISymbol symbol)
        {
            var location = symbol.Locations.FirstOrDefault(item => item.IsInSource);
            if (location == null)
                return SymbolText.Signature(symbol);

            return $"{location.SourceTree!.FilePath}:{location.SourceSpan.Start}";
        }

#endregion

#region Position

        private static async Task<ISymbol> ResolvePositionAsync(LoadedSolution loaded, SymbolQuery query,
            string baseDirectory, CancellationToken cancellation)
        {
            var file = ResolveFile(loaded, query.File!, baseDirectory);
            var documents = loaded.FindDocuments(file);
            if (documents.Count == 0)
                throw new SymbolException($"файла нет в решении: {query.File}");

            var document = documents[0];
            var text = await document.GetTextAsync(cancellation);
            if (query.Line < 1 || query.Line > text.Lines.Count)
                throw new SymbolException($"{query.Text}: в файле {text.Lines.Count} строк");

            var line = text.Lines[query.Line - 1];
            var model = await document.GetSemanticModelAsync(cancellation)
                ?? throw new SymbolException($"{query.Text}: нет семантической модели");
            var symbol = query.Column == null
                ? FindOnLine(model, line.Span, cancellation)
                : await FindAtColumnAsync(document, line, query.Column.Value, cancellation);
            if (symbol == null)
                throw new SymbolException($"{query.Text}: в позиции нет символа");

            return Normalize(symbol);
        }

        private static string ResolveFile(LoadedSolution loaded, string file, string baseDirectory)
        {
            var fromBase = Path.GetFullPath(Path.Combine(baseDirectory, file));
            if (File.Exists(fromBase) || loaded.Contains(fromBase))
                return fromBase;

            return Path.GetFullPath(Path.Combine(loaded.Root, file));
        }

        private static async Task<ISymbol?> FindAtColumnAsync(Document document, TextLine line, int column,
            CancellationToken cancellation)
        {
            var position = Math.Min(line.Start + Math.Max(column - 1, 0), line.End);
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position, cancellation);
            if (symbol == null && position > line.Start)
                symbol = await SymbolFinder.FindSymbolAtPositionAsync(document, position - 1, cancellation);

            return symbol;
        }

        // Без колонки: первое объявление в строке, иначе первый символ, на который ссылается идентификатор
        private static ISymbol? FindOnLine(SemanticModel model, TextSpan span, CancellationToken cancellation)
        {
            var root = model.SyntaxTree.GetRoot(cancellation);
            var identifiers = root.DescendantTokens(span)
                .Where(token => token.IsKind(SyntaxKind.IdentifierToken) && span.Contains(token.SpanStart))
                .ToList();
            foreach (var token in identifiers)
            {
                var declared = model.GetDeclaredSymbol(token.Parent!, cancellation);
                if (declared != null)
                    return declared;
            }

            foreach (var token in identifiers)
            {
                var referenced = model.GetSymbolInfo(token.Parent!, cancellation);
                var symbol = referenced.Symbol ?? referenced.CandidateSymbols.FirstOrDefault();
                if (symbol != null)
                    return symbol;
            }

            return null;
        }

        private static ISymbol Normalize(ISymbol symbol)
        {
            return symbol switch
            {
                IAliasSymbol alias => alias.Target,
                IMethodSymbol { ReducedFrom: { } reduced } => reduced.OriginalDefinition,
                _ => symbol.OriginalDefinition,
            };
        }

#endregion
    }
}
