using CSR.Core.Arch;

namespace CSR.Core.Style
{
    // Запреты из конфига: маска имени и текст альтернативы. Маски собираются один раз на файл
    public class ForbiddenNames
    {
        private readonly List<KeyValuePair<NameMask, string>> m_Entries;

        public ForbiddenNames(IReadOnlyDictionary<string, string> masks)
        {
            m_Entries = masks.Select(pair => new KeyValuePair<NameMask, string>(NameMask.Get([pair.Key]), pair.Value)).ToList();
        }

        public bool IsEmpty
            => m_Entries.Count == 0;

        // Первая подходящая маска: text - ее альтернатива (может быть пустой)
        public bool TryFind(string name, out string text)
        {
            foreach (var entry in m_Entries)
            {
                if (!entry.Key.IsMatch(name))
                    continue;

                text = entry.Value;
                return true;
            }

            text = "";
            return false;
        }

        // Сообщение находки с альтернативой: "что: чем заменить"; пустая альтернатива - только что
        public static string WithUse(string message, string use)
            => use.Length > 0 ? $"{message}: {use}" : message;
    }
}
