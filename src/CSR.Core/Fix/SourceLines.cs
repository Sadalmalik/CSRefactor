using System.Text;

namespace CSR.Core.Fix
{
    // Текст файла построчно: у каждой строки свой перевод ("\r\n", "\n" или пусто у последней) -
    // замена строк не меняет переводы соседних строк
    public class SourceLines
    {
        private readonly List<string> m_Lines = new();
        private readonly List<string> m_Endings = new();

        private SourceLines()
        {
        }

        public int Count
            => m_Lines.Count;

        public static SourceLines Parse(string text)
        {
            var lines = new SourceLines();
            var start = 0;
            while (start < text.Length)
            {
                var end = text.IndexOf('\n', start);
                if (end < 0)
                {
                    lines.Add(text.Substring(start), "");
                    break;
                }

                bool crlf = end > start && text[end - 1] == '\r';
                lines.Add(text.Substring(start, (crlf ? end - 1 : end) - start), crlf ? "\r\n" : "\n");
                start = end + 1;
            }

            return lines;
        }

        // Номер строки с 1
        public string this[int line]
            => m_Lines[line - 1];

        // Строки line..endLine (с 1) заменяются новыми; пустой список - строки удаляются.
        // Новые строки получают перевод первой заменяемой строки, последняя - перевод последней заменяемой
        public void Replace(int line, int endLine, IReadOnlyList<string> replacement)
        {
            var first = line - 1;
            var count = endLine - line + 1;
            var newLine = m_Endings[first].Length > 0 ? m_Endings[first] : GetNewLine();
            var lastEnding = m_Endings[endLine - 1];
            m_Lines.RemoveRange(first, count);
            m_Endings.RemoveRange(first, count);
            for (var i = 0; i < replacement.Count; i++)
            {
                m_Lines.Insert(first + i, replacement[i]);
                m_Endings.Insert(first + i, i == replacement.Count - 1 ? lastEnding : newLine);
            }
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            for (var i = 0; i < m_Lines.Count; i++)
                text.Append(m_Lines[i]).Append(m_Endings[i]);

            return text.ToString();
        }

        private void Add(string line, string ending)
        {
            m_Lines.Add(line);
            m_Endings.Add(ending);
        }

        // Перевод строки файла: первый встреченный, иначе "\n"
        private string GetNewLine()
        {
            foreach (var ending in m_Endings)
            {
                if (ending.Length > 0)
                    return ending;
            }

            return "\n";
        }
    }
}
