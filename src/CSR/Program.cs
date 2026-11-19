namespace CSR
{
    public static class Program
    {
        private const string Usage = """
            csr - анализ и рефакторинг C# для ИИ-агентов

            Команды (в разработке):
              style    проверка стиля: форматирование, имена, порядок членов, комментарии
              arch     проверка архитектурных признаков
              format   форматирование изменённых строк
              refs     все ссылки на символ
              rename   семантическое переименование с защитой сериализуемых имён
              metrics  цикломатическая сложность и CRAP

            csr help - эта справка
            """;

        public static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (args.Length == 0 || args[0] is "help" or "-h" or "--help")
            {
                Console.WriteLine(Usage);
                return 0;
            }

            Console.Error.WriteLine($"Команда '{args[0]}' ещё не реализована");
            return 2;
        }
    }
}
