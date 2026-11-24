using CSR.Reports.Config;

namespace CSR
{
    // Разбор аргументов команды: позиционные значения, опции со значением (--name value) и флаги (--name)
    public class CommandLine
    {
        private readonly List<string> m_Positional = new();
        private readonly Dictionary<string, List<string>> m_Options = new();
        private readonly HashSet<string> m_Flags = new();

        public IReadOnlyList<string> Positional
            => m_Positional;

        public static CommandLine Parse(IEnumerable<string> args, ISet<string> optionNames, ISet<string> flagNames)
        {
            var commandLine = new CommandLine();
            using var enumerator = args.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var arg = enumerator.Current;
                if (!arg.StartsWith("--", StringComparison.Ordinal))
                {
                    commandLine.m_Positional.Add(arg);
                    continue;
                }

                if (flagNames.Contains(arg))
                {
                    commandLine.m_Flags.Add(arg);
                    continue;
                }

                if (!optionNames.Contains(arg))
                    throw new UsageException($"неизвестная опция '{arg}'");

                if (!enumerator.MoveNext())
                    throw new UsageException($"у опции '{arg}' нет значения");

                if (!commandLine.m_Options.TryGetValue(arg, out List<string>? values))
                {
                    values = new List<string>();
                    commandLine.m_Options[arg] = values;
                }

                values.Add(enumerator.Current);
            }

            return commandLine;
        }

        public bool HasFlag(string name)
            => m_Flags.Contains(name);

        public IReadOnlyList<string> GetOptions(string name)
            => m_Options.TryGetValue(name, out List<string>? values) ? values : Array.Empty<string>();

        public string? GetOption(string name)
        {
            if (!m_Options.TryGetValue(name, out List<string>? values))
                return null;

            if (values.Count > 1)
                throw new UsageException($"опция '{name}' задана несколько раз");

            return values[0];
        }

        // Опции таблицы ключей конфига поверх его значений; ошибка значения - ошибка запуска
        public void ReadOptions<T>(OptionTable<T> table, T target)
        {
            try
            {
                table.ReadOptions(GetOption, target);
            }
            catch (ConfigException exception)
            {
                throw new UsageException(exception.Message);
            }
        }
    }

    public class UsageException : Exception
    {
        public UsageException(string message)
            : base(message)
        {
        }
    }
}
