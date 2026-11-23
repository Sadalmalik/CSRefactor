namespace CSR.Core.Similar
{
    // Параметры поиска похожего кода (similar.jsonc и ключи командной строки)
    public class SimilarOptions
    {
        // Виды единиц, которые ищутся
        public HashSet<UnitKind> Kinds { get; set; } =
        [
            UnitKind.Member,
            UnitKind.Fragment,
            UnitKind.ParameterSet,
            UnitKind.FieldSet,
            UnitKind.Type,
        ];

#region L1

        // Порог коэффициента Жаккара по отпечаткам формы
        public double Threshold { get; set; } = 0.82;

        public int MinLines { get; set; } = 4;

        public int MinNodes { get; set; } = 20;

        // Наименьшее число разных поддеревьев формы: цепочка одинаковых простых операторов (x = a; y = b; ...) мала,
        // хотя узлов в ней много
        public int MinShapes { get; set; } = 20;

        // Операторов в окне фрагмента; меньше 2 - фрагменты не ищутся
        public int FragmentStatements { get; set; } = 4;

#endregion


#region L2

        // Порог коэффициента Жаккара наборов и наименьшее число общих элементов пары
        public double SetThreshold { get; set; } = 0.5;

        public int MinShared { get; set; } = 3;

        // Синонимы имен элементов набора (pos -> position): имя в нижнем регистре без префикса поля
        public Dictionary<string, string> Synonyms { get; set; } = new();

#endregion


#region L3

        // Порог взвешенной оценки пары типов
        public double TypeThreshold { get; set; } = 0.75;

        // Наименьшее число элементов профиля типа (полей и членов)
        public int MinTypeMembers { get; set; } = 4;

        // Пара типов по общему ядру: членов одной формы не меньше; 0 - не искать (TypeCores)
        public int MinCoreMembers { get; set; } = 4;

        // Веса частей оценки типа; часть, пустая у обоих типов, не учитывается
        public TypeWeights Weights { get; set; } = new();

#endregion

        // Маски путей файлов области и исключений (* - любые символы, разделитель /)
        public List<string> Paths { get; set; } = new();

        public List<string> ExcludePaths { get; set; } = new();
    }

    public class TypeWeights
    {
        public double Bases { get; set; } = 1;

        public double Overrides { get; set; } = 1;

        public double Fields { get; set; } = 1;

        public double Signatures { get; set; } = 1;

        public double Bodies { get; set; } = 2;
    }
}
