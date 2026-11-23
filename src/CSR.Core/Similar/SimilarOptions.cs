namespace CSR.Core.Similar
{
    // Параметры поиска похожего кода (similar.jsonc и ключи командной строки)
    public class SimilarOptions
    {
        // Порог коэффициента Жаккара по отпечаткам формы
        public double Threshold { get; set; } = 0.82;

        public int MinLines { get; set; } = 4;

        public int MinNodes { get; set; } = 20;

        // Наименьшее число разных поддеревьев формы: цепочка одинаковых простых операторов (x = a; y = b; ...) мала,
        // хотя узлов в ней много
        public int MinShapes { get; set; } = 20;

        // Операторов в окне фрагмента; меньше 2 - фрагменты не ищутся
        public int FragmentStatements { get; set; } = 4;

        // Маски путей файлов области и исключений (* - любые символы, разделитель /)
        public List<string> Paths { get; set; } = new();

        public List<string> ExcludePaths { get; set; } = new();
    }
}
