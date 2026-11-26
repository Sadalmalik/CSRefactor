using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Text
{
    // Геометрия строк: номер, текст, длина и колонка позиции, отступ, перевод строки, сдвиг отступа
    public static class TextLayout
    {
        // Отступ строки, в которой стоит позиция
        public static string GetIndent(SourceText text, int position)
        {
            var content = text.Lines.GetLineFromPosition(position).ToString();
            return content.Substring(0, content.Length - content.TrimStart().Length);
        }

        // Отступ на уровень глубже: отступ кончается табом - таб, иначе 4 пробела
        public static string Deeper(string indent)
            => indent + (indent.EndsWith("\t", StringComparison.Ordinal) ? "\t" : "    ");

        public static bool IsBlank(SourceText text, int lineNumber)
            => string.IsNullOrWhiteSpace(text.Lines[lineNumber].ToString());

        // Перевод строки для текста у строки lineNumber: ее перевод, иначе первый в файле, иначе \n
        public static string GetLineBreak(SourceText text, int lineNumber)
        {
            var line = text.Lines[lineNumber];
            if (line.EndIncludingLineBreak > line.End)
                return text.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));

            foreach (var other in text.Lines)
            {
                if (other.EndIncludingLineBreak > other.End)
                    return text.ToString(TextSpan.FromBounds(other.End, other.EndIncludingLineBreak));
            }

            return "\n";
        }

        // Строка без перевода со сдвигом отступа from -> to: пустая - пустой, директива # и строка с другим отступом - как есть
        public static string Reindent(string content, string from, string to)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            if (content.TrimStart().StartsWith("#", StringComparison.Ordinal))
                return content;

            return content.StartsWith(from, StringComparison.Ordinal) ? to + content.Substring(from.Length) : content;
        }

        public static int GetLineNumber(SourceText text, int position)
            => text.Lines.GetLineFromPosition(position).LineNumber;

        public static string GetLine(SourceText text, int lineNumber)
            => text.Lines[lineNumber].ToString();

        // Длина строки, в которой стоит позиция
        public static int GetLineLength(SourceText text, int position)
            => text.Lines.GetLineFromPosition(position).Span.Length;

        // Колонка позиции с 0
        public static int GetColumn(SourceText text, int position)
            => position - text.Lines.GetLineFromPosition(position).Start;

        // Узел или токен в одной строке
        public static bool IsSingleLine(SourceText text, TextSpan span)
            => GetLineNumber(text, span.Start) == GetLineNumber(text, span.End);
    }
}
