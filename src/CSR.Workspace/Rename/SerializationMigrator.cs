using CSR.Core.Text;
using CSR.Workspace.Refactor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Rename
{
    // --migrate: старое имя остается для сохраненных данных.
    // Unity - [FormerlySerializedAs("старое")], Json - [JsonProperty("старое")], DataMember - Name = "старое"
    public class SerializationMigration
    {
        private readonly Dictionary<DocumentId, List<TextChange>> m_Changes;

        public SerializationMigration(Solution solution, Dictionary<DocumentId, List<TextChange>> changes)
        {
            Solution = solution;
            m_Changes = changes;
        }

        public Solution Solution { get; }

        // Позиция в документе до миграции -> после
        public int MapPosition(DocumentId document, int position)
        {
            if (!m_Changes.TryGetValue(document, out List<TextChange>? changes))
                return position;

            var shift = changes
                .Where(change => change.Span.Start <= position)
                .Sum(change => change.NewText!.Length - change.Span.Length);
            return position + shift;
        }
    }

    public static class SerializationMigrator
    {
        private const string UnityNamespace = "UnityEngine.Serialization";
        private const string FormerlySerializedAs = "FormerlySerializedAs";

        public static async Task<SerializationMigration> ApplyAsync(
            Solution solution,
            ISymbol symbol,
            IReadOnlyList<SerializedName> names,
            CancellationToken cancellation)
        {
            var changes = new Dictionary<DocumentId, List<TextChange>>();
            foreach (var name in names)
            {
                if (name.Kind == SerializedKind.UnityField)
                    await AddUnityAsync(solution, symbol, name.BackingField, changes, cancellation);
                else if (name.Attribute?.ApplicationSyntaxReference is { } reference)
                    await AddAttributeNameAsync(solution, symbol.Name, name.Kind, reference, changes, cancellation);
            }

            var migrated = solution;
            foreach (var (documentId, documentChanges) in changes)
            {
                documentChanges.Sort((left, right) => left.Span.Start.CompareTo(right.Span.Start));
                var text = await solution.GetDocument(documentId)!.GetTextAsync(cancellation);
                migrated = migrated.WithDocumentText(documentId, text.WithChanges(documentChanges));
            }

            return new SerializationMigration(migrated, changes);
        }

        private static async Task AddUnityAsync(
            Solution solution,
            ISymbol symbol,
            bool backingField,
            Dictionary<DocumentId, List<TextChange>> changes,
            CancellationToken cancellation)
        {
            foreach (var reference in symbol.DeclaringSyntaxReferences)
            {
                var syntax = await reference.GetSyntaxAsync(cancellation);
                var member = syntax.FirstAncestorOrSelf<MemberDeclarationSyntax>();
                var document = solution.GetDocument(reference.SyntaxTree);
                if (member == null || document == null)
                    continue;

                var text = await document.GetTextAsync(cancellation);
                var root = (CompilationUnitSyntax)await reference.SyntaxTree.GetRootAsync(cancellation);
                var attributeName = GetUnityAttributeName(root, member, changes, document.Id, text);
                var oldName = backingField ? $"<{symbol.Name}>k__BackingField" : symbol.Name;
                var target = backingField ? "field: " : "";
                var attribute = $"[{target}{attributeName}(\"{oldName}\")]";
                Add(changes, document.Id, CreateLineInsert(text, member.SpanStart, attribute));
            }
        }

        // Короткое имя, если using UnityEngine.Serialization есть или его можно добавить к usings файла;
        // иначе полное
        private static string GetUnityAttributeName(
            CompilationUnitSyntax root,
            MemberDeclarationSyntax member,
            Dictionary<DocumentId, List<TextChange>> changes,
            DocumentId document,
            SourceText text)
        {
            var usings = member.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .SelectMany(namespaceDeclaration => namespaceDeclaration.Usings)
                .Concat(root.Usings);
            if (usings.Any(item => item.Alias == null && item.Name?.ToString() == UnityNamespace))
                return FormerlySerializedAs;

            if (root.Usings.Count == 0)
                return $"{UnityNamespace}.{FormerlySerializedAs}";

            bool exists = changes.TryGetValue(document, out List<TextChange>? documentChanges)
                && documentChanges.Any(change => change.NewText!.Contains($"using {UnityNamespace};"));
            if (!exists)
                Add(changes, document, CreateUsingInsert(root, text));

            return FormerlySerializedAs;
        }

        private static TextChange CreateUsingInsert(CompilationUnitSyntax root, SourceText text)
        {
            var statement = $"using {UnityNamespace};";
            var next = root.Usings.FirstOrDefault(item =>
                item.StaticKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.None)
                && item.Alias == null
                && string.CompareOrdinal(item.Name?.ToString(), UnityNamespace) > 0);

            // Перевод строки - соседнего using: перед которым или после которого идет вставка
            if (next != null)
            {
                var nextLine = text.Lines.GetLineFromPosition(next.SpanStart);
                var nextBreak = TextLayout.GetLineBreak(text, nextLine.LineNumber);
                return new TextChange(new TextSpan(nextLine.Start, 0), statement + nextBreak);
            }

            var last = text.Lines.GetLineFromPosition(root.Usings.Last().Span.End);
            var lineBreak = TextLayout.GetLineBreak(text, last.LineNumber);
            return new TextChange(new TextSpan(last.EndIncludingLineBreak, 0), statement + lineBreak);
        }

        private static async Task AddAttributeNameAsync(
            Solution solution,
            string oldName,
            SerializedKind kind,
            SyntaxReference reference,
            Dictionary<DocumentId, List<TextChange>> changes,
            CancellationToken cancellation)
        {
            var document = solution.GetDocument(reference.SyntaxTree);
            if (document == null || await reference.GetSyntaxAsync(cancellation) is not AttributeSyntax attribute)
                return;

            var literal = $"\"{oldName}\"";
            var argument = kind == SerializedKind.Json ? literal : $"Name = {literal}";
            var arguments = attribute.ArgumentList;
            TextChange change;
            if (arguments == null)
                change = new TextChange(new TextSpan(attribute.Name.Span.End, 0), $"({argument})");
            else if (arguments.Arguments.Count == 0)
                change = new TextChange(new TextSpan(arguments.OpenParenToken.Span.End, 0), argument);
            else if (kind == SerializedKind.Json)
                change = new TextChange(new TextSpan(arguments.Arguments[0].SpanStart, 0), argument + ", ");
            else
                change = new TextChange(new TextSpan(arguments.Arguments.Last().Span.End, 0), ", " + argument);

            Add(changes, document.Id, change);
        }

        // Атрибут отдельной строкой перед членом с его отступом; если перед членом в строке есть код - в строку
        private static TextChange CreateLineInsert(SourceText text, int memberStart, string attribute)
        {
            var line = text.Lines.GetLineFromPosition(memberStart);
            var prefix = text.ToString(TextSpan.FromBounds(line.Start, memberStart));
            if (!string.IsNullOrWhiteSpace(prefix))
                return new TextChange(new TextSpan(memberStart, 0), attribute + " ");

            var lineBreak = TextLayout.GetLineBreak(text, line.LineNumber);
            return new TextChange(new TextSpan(line.Start, 0), prefix + attribute + lineBreak);
        }

        private static void Add(
            Dictionary<DocumentId, List<TextChange>> changes,
            DocumentId document,
            TextChange change)
        {
            if (!changes.TryGetValue(document, out List<TextChange>? list))
            {
                list = [];
                changes[document] = list;
            }

            list.Add(change);
        }
    }
}
