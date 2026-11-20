using CSR.Commands;
using CSR.Reports.Config;

namespace CSR
{
    public static class Program
    {
        private const string Usage = """
            csr - анализ и рефакторинг C# для ИИ-агентов

            Команды:
              metrics  метрики по синтаксису: сложность, длина, вложенность, размеры типов и файлов

            В разработке:
              style    проверка стиля: форматирование, имена, порядок членов, комментарии
              arch     проверка архитектурных признаков
              format   форматирование изменённых строк
              refs     все ссылки на символ
              rename   семантическое переименование с защитой сериализуемых имён

            csr help [команда] - справка
            Коды возврата: 0 - успех, 2 - ошибка запуска
            """;

        public static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (args.Length == 0 || args[0] is "help" or "-h" or "--help")
                return PrintHelp(args.Length > 1 ? args[1] : null);

            try
            {
                return args[0] switch
                {
                    "metrics" => MetricsCommand.Run(args.Skip(1)),
                    _ => throw new UsageException($"команда '{args[0]}' не реализована"),
                };
            }
            catch (UsageException exception)
            {
                Console.Error.WriteLine($"csr: {exception.Message}");
                return 2;
            }
            catch (ConfigException exception)
            {
                Console.Error.WriteLine($"csr: конфиг: {exception.Message}");
                return 2;
            }
            catch (System.Text.Json.JsonException exception)
            {
                Console.Error.WriteLine($"csr: конфиг: {exception.Message}");
                return 2;
            }
        }

        private static int PrintHelp(string? command)
        {
            Console.WriteLine(command == "metrics" ? MetricsCommand.Usage : Usage);
            return 0;
        }
    }
}
