using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CSR.Core.Arch
{
    // Набор масок имен или путей: * - любые символы. Маски одного списка собираются в одно выражение и кэшируются
    public class NameMask
    {
        private static readonly ConcurrentDictionary<string, NameMask> Cache = new();

        private readonly Regex? m_Pattern;

        private NameMask(IReadOnlyList<string> masks, bool isIgnoreCase)
        {
            if (masks.Count == 0)
                return;

            var options = RegexOptions.CultureInvariant | (isIgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            var alternatives = masks.Select(mask => Regex.Escape(mask).Replace("\\*", ".*"));
            m_Pattern = new Regex("^(?:" + string.Join("|", alternatives) + ")$", options);
        }

        public bool IsEmpty
            => m_Pattern == null;

        public static NameMask Get(IReadOnlyList<string> masks, bool isIgnoreCase = false)
        {
            var key = (isIgnoreCase ? "i\n" : "c\n") + string.Join("\n", masks);
            return Cache.GetOrAdd(key, _ => new NameMask(masks, isIgnoreCase));
        }

        public bool IsMatch(string text)
            => m_Pattern != null && m_Pattern.IsMatch(text);
    }
}
