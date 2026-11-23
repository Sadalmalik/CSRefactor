namespace CSR.Core.Checks
{
    public enum Severity
    {
        Hint,
        Warning,
        Error,
    }

    // Находка проверки. Строки и колонка - с 1
    public class Finding
    {
        public required string Check { get; init; }

        // Метка правила из конфига (ID гайда); null - конфиг метку не задал
        public string? Rule { get; init; }

        public required Severity Severity { get; init; }

        public required string Message { get; init; }

        public required string File { get; init; }

        public required int Line { get; init; }

        public required int EndLine { get; init; }

        public required int Column { get; init; }

        // Новый текст строк Line..EndLine целиком; пустой список - удалить строки; null - замены нет
        public IReadOnlyList<string>? Replacement { get; init; }

        // Уровень по ключу конфига (error, warning, hint); неизвестный ключ - null
        public static Severity? ParseSeverity(string key)
        {
            return key switch
            {
                "error" => Severity.Error,
                "warning" => Severity.Warning,
                "hint" => Severity.Hint,
                _ => null,
            };
        }

        public static string GetSeverityKey(Severity severity)
        {
            return severity switch
            {
                Severity.Hint => "hint",
                Severity.Warning => "warning",
                _ => "error",
            };
        }
    }
}
