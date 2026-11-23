using System.Collections.Concurrent;

namespace CSR.Core.Similar
{
    // Пара похожих единиц. У фрагмента после склейки - число совпавших окон подряд
    public class SimilarPair
    {
        public required SimilarUnit Left { get; init; }

        public required SimilarUnit Right { get; init; }

        public required double Score { get; init; }

        public int Windows { get; init; } = 1;

        // L3: членов одной формы (общее ядро) - пара найдена по ядру, если оценка ниже порога
        public int SharedMembers { get; init; }
    }

    // Пары единиц одного вида. Кандидаты - через префиксный фильтр (AllPairs) с порогом сравнения вида: отпечатки
    // единицы упорядочены от редких к частым, две единицы с J >= t обязательно делят отпечаток в префиксах длины
    // n - ceil(t * n) + 1. Отбор точный, без пропусков: частые отпечатки (символ, литерал, база MonoBehaviour) в
    // префикс почти не попадают, поэтому списки кандидатов короткие. Оценку пары дает сравнение вида
    public static class SimilarFinder
    {
        // isProbe - единицы, для которых ищутся пары (--changed); null - все пары всех единиц
        public static List<SimilarPair> Find(
            IReadOnlyList<SimilarUnit> units,
            Func<UnitKind, UnitComparer> getComparer,
            Func<SimilarUnit, bool>? isProbe = null)
        {
            var pairs = new List<SimilarPair>();
            foreach (var group in units.GroupBy(unit => unit.Kind))
                pairs.AddRange(FindInKind(group.ToList(), getComparer(group.Key), isProbe));

            return pairs;
        }

        public static double GetJaccard(ulong[] left, ulong[] right)
            => UnitComparer.GetJaccard(left, right);

        private static List<SimilarPair> FindInKind(
            List<SimilarUnit> units,
            UnitComparer comparer,
            Func<SimilarUnit, bool>? isProbe)
        {
            double threshold = comparer.CandidateThreshold;
            var prefixes = GetPrefixes(units, threshold);
            var index = new Dictionary<ulong, List<int>>();
            for (int i = 0; i < units.Count; i++)
            {
                foreach (ulong token in prefixes[i])
                {
                    if (!index.TryGetValue(token, out List<int>? list))
                    {
                        list = new List<int>();
                        index[token] = list;
                    }

                    list.Add(i);
                }
            }

            var found = new ConcurrentBag<SimilarPair>();
            Parallel.For(0, units.Count, i =>
            {
                bool isLeftProbe = isProbe == null || isProbe(units[i]);
                if (!isLeftProbe)
                    return;

                var candidates = new HashSet<int>();
                foreach (ulong token in prefixes[i])
                {
                    foreach (int j in index[token])
                    {
                        // пара - один раз: при двух проверяемых единицах ее берет меньший номер
                        bool isRightProbe = isProbe == null || isProbe(units[j]);
                        if (j == i || isRightProbe && j < i)
                            continue;

                        candidates.Add(j);
                    }
                }

                foreach (int j in candidates)
                {
                    var pair = Compare(units[i], units[j], comparer);
                    if (pair != null)
                        found.Add(pair);
                }
            });

            return found.ToList();
        }

        private static SimilarPair? Compare(SimilarUnit left, SimilarUnit right, UnitComparer comparer)
        {
            int small = Math.Min(left.Fingerprints.Length, right.Fingerprints.Length);
            int large = Math.Max(left.Fingerprints.Length, right.Fingerprints.Length);
            if (small < comparer.CandidateThreshold * large || IsOverlapping(left, right))
                return null;

            double? score = comparer.Score(left, right);
            if (score == null)
                return null;

            bool isOrdered = string.CompareOrdinal(left.File, right.File) < 0
                || left.File == right.File && left.SpanStart < right.SpanStart;
            return isOrdered
                ? new SimilarPair { Left = left, Right = right, Score = score.Value }
                : new SimilarPair { Left = right, Right = left, Score = score.Value };
        }

        // Перекрытые единицы - не повтор: лямбда внутри своего метода, окна одного блока со сдвигом меньше окна
        private static bool IsOverlapping(SimilarUnit left, SimilarUnit right)
            => left.File == right.File && left.SpanStart < right.SpanEnd && right.SpanStart < left.SpanEnd;

        // Префиксы отпечатков: порядок - по числу единиц с отпечатком (редкие первыми), затем по значению
        private static List<ulong[]> GetPrefixes(List<SimilarUnit> units, double threshold)
        {
            var frequency = new Dictionary<ulong, int>();
            foreach (var unit in units)
            {
                foreach (ulong token in unit.Fingerprints)
                    frequency[token] = frequency.TryGetValue(token, out int count) ? count + 1 : 1;
            }

            var prefixes = new List<ulong[]>(units.Count);
            foreach (var unit in units)
            {
                int length = unit.Fingerprints.Length;
                int prefix = Math.Max(1, length - (int)Math.Ceiling(threshold * length) + 1);
                var ordered = unit.Fingerprints
                    .OrderBy(token => frequency[token])
                    .ThenBy(token => token)
                    .Take(Math.Min(prefix, length))
                    .ToArray();
                prefixes.Add(ordered);
            }

            return prefixes;
        }
    }
}
