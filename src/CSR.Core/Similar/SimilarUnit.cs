namespace CSR.Core.Similar
{
    // Вид единицы сравнения. L1: член с телом и фрагмент (окно подряд идущих операторов). Следующие шаги
    // (наборы параметров, классы, системы) добавляются новыми видами со своими отпечатками
    public enum UnitKind
    {
        Member,
        Fragment,
    }

    // Единица сравнения: место в коде и отпечатки нормализованной формы (отсортированы, без повторов)
    public class SimilarUnit
    {
        public required UnitKind Kind { get; init; }

        public required string File { get; init; }

        // Член для человека: Type.Method, Type.Method.Local, Type.Method (лямбда)
        public required string Member { get; init; }

        public required int StartLine { get; init; }

        public required int EndLine { get; init; }

        public required int NodeCount { get; init; }

        public required ulong[] Fingerprints { get; init; }

        // Позиция в файле: вложенность единиц (лямбда внутри метода) и перекрытие фрагментов
        public required int SpanStart { get; init; }

        public required int SpanEnd { get; init; }

        // Фрагмент: блок, номер первого оператора окна и член, которому блок принадлежит
        public int BlockStart { get; init; } = -1;

        public int StatementIndex { get; init; } = -1;

        public SimilarUnit? Owner { get; init; }

        public int LineCount
            => EndLine - StartLine + 1;
    }
}
