namespace CSR.Commands
{
    // Итог анализа: отчет (stdout или --out), сводка в stderr (время дописывается), строки после сводки, код возврата
    public class FileResult
    {
        public required string Text { get; init; }

        public required string Summary { get; init; }

        public string Details { get; init; } = "";

        public int ExitCode { get; init; }
    }
}
