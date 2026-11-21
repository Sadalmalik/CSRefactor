using Microsoft.CodeAnalysis.Text;

namespace CSR.Workspace.Refactor
{
    // Правки одного документа в координатах исходного текста: замены и вставки (TextChange) и удаление
    // целых строк с выравниванием пустых строк на стыке. Правки не пересекаются
    public class DocumentEdit
    {
        private readonly List<TextChange> m_Changes = [];
        private readonly List<TextSpan> m_Removed = [];

        public DocumentEdit(SourceText text)
        {
            Text = text;
        }

        public SourceText Text { get; }

        public bool IsEmpty
            => m_Changes.Count == 0 && m_Removed.Count == 0;

        public void Replace(TextSpan span, string text)
            => m_Changes.Add(new TextChange(span, text));

        public void Insert(int position, string text)
            => m_Changes.Add(new TextChange(new TextSpan(position, 0), text));

        public void RemoveLines(TextSpan lines)
            => m_Removed.Add(lines);

        // Замены, затем удаления строк по сдвинутым координатам
        public SourceText Apply()
        {
            var changes = m_Changes.OrderBy(change => change.Span.Start).ToList();
            var text = Text.WithChanges(changes);
            var removed = m_Removed.Select(span => Shift(span, changes));
            return SourceLines.RemoveLines(text, removed);
        }

        private static TextSpan Shift(TextSpan span, List<TextChange> changes)
        {
            var delta = changes
                .Where(change => change.Span.End <= span.Start)
                .Sum(change => change.NewText!.Length - change.Span.Length);
            var inside = changes
                .Where(change => change.Span.Start >= span.Start && change.Span.End <= span.End)
                .Sum(change => change.NewText!.Length - change.Span.Length);
            return new TextSpan(span.Start + delta, span.Length + inside);
        }
    }
}
