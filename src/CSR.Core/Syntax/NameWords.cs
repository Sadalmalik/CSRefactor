using System.Text;
using System.Text.RegularExpressions;

namespace CSR.Core.Syntax
{
    // Разбор имени на слова для проверок обоих режимов: части через '_' (и другие не буквы и не цифры) и границы
    // регистра; цифры - часть слова
    public static class NameWords
    {
        private static readonly Regex FieldPrefix = new("^(?:[a-z]{1,2}_|_+)", RegexOptions.CultureInvariant);

        // IsUIVisible -> Is, UI, Visible; m_isOn -> m, is, On; Vector3Pool -> Vector3, Pool
        public static IReadOnlyList<string> Split(string name)
        {
            var words = new List<string>();
            var word = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char current = name[i];
                if (current == '_' || !char.IsLetterOrDigit(current))
                {
                    Flush(words, word);
                    continue;
                }

                if (word.Length > 0 && IsBoundary(name, i))
                    Flush(words, word);

                word.Append(current);
            }

            Flush(words, word);
            return words;
        }

        // Слова идентификатора без префикса поля: m_EnterRadius -> Enter, Radius; MAX_SPEED -> MAX, SPEED
        public static IReadOnlyList<string> GetWords(string name)
            => Split(StripFieldPrefix(name));

        // Есть ли в словах имени фраза подряд (DeadZone -> Dead, Zone), без учета регистра
        public static bool ContainsPhrase(IReadOnlyList<string> words, string phrase)
        {
            var parts = GetWords(phrase);
            if (parts.Count == 0)
                return false;

            for (int start = 0; start + parts.Count <= words.Count; start++)
            {
                bool isMatch = true;
                for (int i = 0; i < parts.Count && isMatch; i++)
                    isMatch = string.Equals(words[start + i], parts[i], StringComparison.OrdinalIgnoreCase);

                if (isMatch)
                    return true;
            }

            return false;
        }

        // Имя без префикса поля: m_Name -> Name, s_Count -> Count, _name -> name
        public static string StripFieldPrefix(string name)
        {
            var match = FieldPrefix.Match(name);
            return match.Length < name.Length ? name.Substring(match.Length) : name;
        }

        // Последнее слово имени без префикса поля: m_ValueList -> List
        public static string GetLastWord(string name)
        {
            var words = Split(StripFieldPrefix(name));
            return words.Count > 0 ? words[words.Count - 1] : name;
        }

        // Множественное число по окончанию: слово из pluralWords или на 's', кроме окончаний singularEndings
        // (Class, Status, Axis)
        public static bool IsPlural(string word, IReadOnlyList<string> pluralWords, IReadOnlyList<string> singularEndings)
        {
            if (pluralWords.Any(plural => string.Equals(plural, word, StringComparison.OrdinalIgnoreCase)))
                return true;

            if (word.Length < 2 || char.ToLowerInvariant(word[word.Length - 1]) != 's')
                return false;

            return !singularEndings.Any(ending => word.EndsWith(ending, StringComparison.OrdinalIgnoreCase));
        }

        // Слово, совпадающее с одним из words без учета регистра
        public static string? FindWord(IEnumerable<string> nameWords, IReadOnlyList<string> words)
        {
            return nameWords.FirstOrDefault(word => words.Any(candidate
                => string.Equals(candidate, word, StringComparison.OrdinalIgnoreCase)));
        }

        // Имя в PascalCase из слов, по частям через '.': MAX_POOL_SIZE -> MaxPoolSize, game.items -> Game.Items,
        // @class -> Class
        public static string ToPascal(string name)
            => string.Join(".", name.Split('.').Select(ToPascalPart));

        // Имя в UPPER_CASE из слов: MaxPoolSize -> MAX_POOL_SIZE
        public static string ToUpperSnake(string name)
            => string.Join("_", Split(name).Select(word => word.ToUpperInvariant()));

        private static string ToPascalPart(string name)
        {
            var words = Split(StripFieldPrefix(name));
            bool isUpper = !name.Any(char.IsLower);
            var result = new StringBuilder();
            foreach (var word in words)
            {
                result.Append(char.ToUpperInvariant(word[0]));
                result.Append(isUpper ? word.Substring(1).ToLowerInvariant() : word.Substring(1));
            }

            return result.ToString();
        }

        // Начало слова: строчная -> заглавная (isOn), заглавная перед заглавной со строчной (UIVisible),
        // цифра -> буква не делит (Vector3Pool делится на заглавной P)
        private static bool IsBoundary(string name, int index)
        {
            char previous = name[index - 1];
            char current = name[index];
            if (!char.IsUpper(current))
                return false;

            if (char.IsLower(previous) || char.IsDigit(previous))
                return true;

            return char.IsUpper(previous) && index + 1 < name.Length && char.IsLower(name[index + 1]);
        }

        private static void Flush(List<string> words, StringBuilder word)
        {
            if (word.Length == 0)
                return;

            words.Add(word.ToString());
            word.Clear();
        }
    }
}
