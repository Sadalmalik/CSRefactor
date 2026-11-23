namespace CSR.Core.Similar
{
    // Вид единицы сравнения. L1: член с телом и фрагмент (окно подряд идущих операторов); L2: набор входных
    // параметров метода и набор полей типа; L3: тип целиком. Единицы разных видов не сравниваются
    public enum UnitKind
    {
        Member,
        Fragment,
        ParameterSet,
        FieldSet,
        Type,
    }

    // Единица сравнения: место в коде и отпечатки (отсортированы, без повторов). У L1 - хеши поддеревьев формы,
    // у L2 - хеши элементов набора, у L3 - хеши элементов профиля типа
    public class SimilarUnit
    {
        public required UnitKind Kind { get; init; }

        public required string File { get; init; }

        // Член или тип для человека: Type.Method, Type.Method.Local, Type.Method (лямбда), Type
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

        // L2, L3: элементы для человека (тип и имя параметра, элемент профиля); отпечатки - их хеши
        public IReadOnlyList<string> Elements { get; init; } = Array.Empty<string>();

        public int LineCount
            => EndLine - StartLine + 1;
    }

    // Тип (L3): части профиля и члены с телом для сопоставления по форме L1
    public class TypeUnit : SimilarUnit
    {
        public required TypeProfile Profile { get; init; }

        public required IReadOnlyList<SimilarUnit> Members { get; init; }
    }

    // Профиль типа по частям: базовые типы, переопределения, поля, формы сигнатур. Элемент повторяется с номером
    // (method bool(1)#2): совпадение считается как у мультимножеств
    public class TypeProfile
    {
        public required ulong[] Bases { get; init; }

        public required ulong[] Overrides { get; init; }

        public required ulong[] Fields { get; init; }

        public required ulong[] Signatures { get; init; }
    }
}
