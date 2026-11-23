using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Style.Naming
{
    // Объявленное имя для правил именования: вид, доступ, модификаторы, объявленный тип, атрибуты и базы по синтаксису
    public class NameSymbol
    {
        public required string Kind { get; init; }

        public required string Name { get; init; }

        public required TextSpan Span { get; init; }

        // Вид словами для сообщения: "закрытое поле", "событие", "пространство"
        public required string Label { get; init; }

        // private, protected, internal, public; null - у вида нет доступа (пространство, параметр, локальная)
        public string? Access { get; init; }

        public IReadOnlyCollection<string> Modifiers { get; init; } = Array.Empty<string>();

        // Тип объявления без пространства имен и '?': bool, List<int>, int[]; null - нет типа или var
        public string? TypeName { get; init; }

        // Атрибуты без пространства имен и суффикса Attribute
        public IReadOnlyList<string> Attributes { get; init; } = Array.Empty<string>();

        // Прямые базовые типы без пространства имен и аргументов типа
        public IReadOnlyList<string> Bases { get; init; } = Array.Empty<string>();

        // Тип вложен в другой тип
        public bool IsNested { get; init; }

        // Пространство имен типа; null - глобальное
        public string? Namespace { get; init; }
    }
}
