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
              style    проверка стиля по конфигу: форматирование, комментарии, идиомы, модификаторы
              fix      применить замены находок style; без --apply - только дифф
              arch     архитектурные признаки по конфигу: находки-решения с вопросом
              refs     все ссылки на символ по решению
              rename   семантическое переименование с защитой сериализуемых имён; без --apply - только дифф
              move     перенос типа в свой файл (.meta Unity - вместе с файлом); без --apply - только дифф
              extract  извлечение метода из строк файла; без --apply - только дифф
              serve    держать решение загруженным для refs, rename, move, extract и arch --solution

            В разработке:
              format   форматирование изменённых строк

            csr help [команда] - справка
            Коды возврата: 0 - успех, 1 - есть находки уровня error (style, arch), 2 - ошибка запуска
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
                    "style" => StyleCommand.Run(args.Skip(1)),
                    "fix" => FixCommand.Run(args.Skip(1)),
                    "arch" => ArchCommand.Run(args.Skip(1)),
                    "refs" => RefsCommand.Run(args.Skip(1)),
                    "rename" => RenameCommand.Run(args.Skip(1)),
                    "move" => MoveCommand.Run(args.Skip(1)),
                    "extract" => ExtractCommand.Run(args.Skip(1)),
                    "serve" => ServeCommand.Run(args.Skip(1)),
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
            var text = command switch
            {
                "metrics" => MetricsCommand.Usage,
                "style" => StyleCommand.Usage,
                "fix" => FixCommand.Usage,
                "arch" => ArchCommand.Usage,
                "refs" => RefsCommand.Usage,
                "rename" => RenameCommand.Usage,
                "move" => MoveCommand.Usage,
                "extract" => ExtractCommand.Usage,
                "serve" => ServeCommand.Usage,
                _ => Usage,
            };

            Console.WriteLine(text);
            return 0;
        }
    }
}
