namespace CSR.Core.Checks
{
    // Замена строк Start..End исходника (индексы SourceText.Lines) строками Lines
    public class LineReplacement
    {
        internal LineReplacement(int start, int end, IReadOnlyList<string> lines)
        {
            Start = start;
            End = end;
            Lines = lines;
        }

        public int Start { get; }

        public int End { get; }

        public IReadOnlyList<string> Lines { get; }
    }
}
