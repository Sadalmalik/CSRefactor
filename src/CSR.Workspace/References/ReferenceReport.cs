namespace CSR.Workspace.References
{
    // Место в коде: путь относительно корня решения (разделитель '/'), строка и колонка с 1
    public class CodeLocation
    {
        public required string File { get; init; }

        public required int Line { get; init; }

        public required int Column { get; init; }

        // Строка кода без отступа
        public required string Text { get; init; }
    }

    public class ReferencePlace : CodeLocation
    {
        // Член или тип, в котором стоит ссылка
        public string? Container { get; init; }

        // Документ генератора кода: переименование его не правит, генератор перегенерирует сам
        public bool Generated { get; init; }

        // Неявная ссылка (foreach, деконструкция и т. п.)
        public bool Implicit { get; init; }

        // Кандидат при ошибке разрешения перегрузки
        public bool Candidate { get; init; }
    }

    public class ReferencedDefinition
    {
        public required string Symbol { get; init; }

        public required string Kind { get; init; }

        public required IReadOnlyList<CodeLocation> Declarations { get; init; }

        public required IReadOnlyList<ReferencePlace> References { get; init; }

        // Символ из метаданных (DLL), объявления в коде решения нет
        public bool Metadata { get; init; }
    }

    public class ReferenceReport
    {
        public required string Query { get; init; }

        // Запрошенный символ и связанные (переопределения, реализации интерфейсов, аксессоры)
        public required IReadOnlyList<ReferencedDefinition> Definitions { get; init; }

        public int ReferenceCount
            => Definitions.Sum(definition => definition.References.Count);

        public int FileCount
            => Definitions
                .SelectMany(definition => definition.References)
                .Select(place => place.File)
                .Distinct()
                .Count();
    }
}
