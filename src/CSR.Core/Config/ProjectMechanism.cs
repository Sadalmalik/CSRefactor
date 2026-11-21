namespace CSR.Core.Config
{
    // Механизм проекта (пул, логгер, таймеры): как распознать его самодельный повтор и чем заменить.
    // Задается в project.jsonc; маски - * любые символы
    public class ProjectMechanism
    {
        public required string Name { get; init; }

        // Что использовать вместо повтора - текст для находки
        public required string Use { get; init; }

        // Маски имен типов-повторов: *Pool
        public IReadOnlyList<string> Types { get; init; } = Array.Empty<string>();

        // Маски типов полей (Stack<*>) и имен методов (Rent*, Return*): повтор - тип с таким полем и методом
        // на каждую маску; без Fields - только методы, без Methods - только поле
        public IReadOnlyList<string> Fields { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Methods { get; init; } = Array.Empty<string>();

        // Маски вызовов в обход механизма: текст вызова без пробелов (Debug.Log*)
        public IReadOnlyList<string> Calls { get; init; } = Array.Empty<string>();

        // Маски типов в new в обход механизма: System.Threading.Timer
        public IReadOnlyList<string> Creates { get; init; } = Array.Empty<string>();

        // Маски путей файлов, где повтор - сама реализация механизма или допустим
        public IReadOnlyList<string> ExcludePaths { get; init; } = Array.Empty<string>();
    }
}
