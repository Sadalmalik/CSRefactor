namespace CSR.Core.Metrics
{
    public enum MetricLevel
    {
        Function,
        Type,
        File,
    }

    // Измеренный элемент: функция, тип или файл. Значения метрик - по ключам MetricKeys
    public class MetricItem
    {
        public required MetricLevel Level { get; init; }

        // method, constructor, accessor, class, struct, file и т. д. - Functions.GetKind, TypeMembers.GetKind
        public required string Kind { get; init; }

        // Имя без пространства имен: Outer.Inner.Method, Outer.Property.get; у файла - путь
        public required string Name { get; init; }

        public required string Namespace { get; init; }

        // У функции - объемлющие типы (Outer.Inner); Name = Owner.Member
        public string Owner { get; init; } = "";

        public string Member
            => Owner.Length == 0 ? Name : Name.Substring(Owner.Length + 1);

        public required string File { get; init; }

        // Строки - с 1
        public required int Line { get; init; }

        public required int EndLine { get; init; }

        public required IReadOnlyDictionary<string, int> Values { get; init; }

        // Только у типа: число членов по видам - TypeMembers
        public IReadOnlyDictionary<string, int>? Members { get; init; }

        // Только у функции, если задано покрытие: доля покрытых точек в процентах и CRAP; null - покрытия нет
        public double? Coverage { get; set; }

        public double? Crap { get; set; }
    }

    public class FileMetrics
    {
        public required MetricItem File { get; init; }

        public required IReadOnlyList<MetricItem> Types { get; init; }

        public required IReadOnlyList<MetricItem> Functions { get; init; }
    }
}
