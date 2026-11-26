using CSR.Workspace.Symbols;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace CSR.Workspace.References
{
    // Все ссылки на символы по решению (SymbolFinder): с переопределениями, реализациями и аксессорами
    public static class ReferenceFinder
    {
        public static async Task<ReferenceReport> FindAsync(
            Solution solution,
            string root,
            string query,
            IReadOnlyList<ISymbol> symbols,
            CancellationToken cancellation = default)
        {
            var definitions = new Dictionary<string, ReferencedDefinition>();
            var models = new Dictionary<DocumentId, SemanticModel?>();
            foreach (var symbol in symbols)
            {
                var found = await SymbolFinder.FindReferencesAsync(symbol, solution, cancellation);
                foreach (var referenced in found)
                {
                    var key = SymbolText.Signature(referenced.Definition);
                    if (definitions.ContainsKey(key) || !IsReported(referenced, symbol, symbols))
                        continue;

                    var places = new List<ReferencePlace>();
                    foreach (var location in referenced.Locations)
                        places.Add(await CreatePlaceAsync(location, root, models, cancellation));

                    definitions[key] = CreateDefinition(referenced.Definition, root, places);
                }
            }

            return new ReferenceReport
            {
                Query = query,
                Definitions = definitions.Values.ToList(),
            };
        }

        // Запрошенный символ, символ со ссылками, реализация или переопределение (то же имя и вид) - даже без ссылок
        private static bool IsReported(ReferencedSymbol referenced, ISymbol symbol, IReadOnlyList<ISymbol> symbols)
        {
            var definition = referenced.Definition;
            if (symbols.Contains(definition, SymbolEqualityComparer.Default) || referenced.Locations.Any())
                return true;

            return definition.Name == symbol.Name && definition.Kind == symbol.Kind
                && definition.Locations.Any(location => location.IsInSource);
        }

        private static ReferencedDefinition CreateDefinition(ISymbol symbol, string root, List<ReferencePlace> places)
        {
            var declarations = symbol.Locations
                .Where(location => location.IsInSource)
                .Select(location => SourceLocationTools.Create(location, root))
                .ToList();
            var ordered = places
                .OrderBy(place => place.File, StringComparer.Ordinal)
                .ThenBy(place => place.Line)
                .ThenBy(place => place.Column)
                .ToList();
            return new ReferencedDefinition
            {
                Symbol = SymbolText.Signature(symbol),
                Kind = SymbolText.Kind(symbol),
                Declarations = declarations,
                References = ordered,
                Metadata = declarations.Count == 0,
            };
        }

        private static async Task<ReferencePlace> CreatePlaceAsync(
            ReferenceLocation reference,
            string root,
            Dictionary<DocumentId, SemanticModel?> models,
            CancellationToken cancellation)
        {
            var location = SourceLocationTools.Create(reference.Location, root);
            if (!models.TryGetValue(reference.Document.Id, out SemanticModel? model))
            {
                model = await reference.Document.GetSemanticModelAsync(cancellation);
                models[reference.Document.Id] = model;
            }

            var container = model?.GetEnclosingSymbol(reference.Location.SourceSpan.Start, cancellation);
            return new ReferencePlace
            {
                File = location.File,
                Line = location.Line,
                Column = location.Column,
                Text = location.Text,
                Container = container == null ? null : SymbolText.Signature(container),
                Generated = reference.Document is SourceGeneratedDocument,
                Implicit = reference.IsImplicit,
                Candidate = reference.IsCandidateLocation,
            };
        }
    }
}
