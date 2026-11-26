namespace CSR.Core.Similar
{
    // Пара типов с общим ядром и число общих членов одной формы
    public class CorePair
    {
        public required TypeUnit Left { get; init; }

        public required TypeUnit Right { get; init; }

        public required int SharedMembers { get; init; }
    }

    // Второй признак L3 - общее ядро: у пары типов не меньше minCoreMembers членов одной формы (отпечатки L1 совпадают
    // целиком). Ловит параллельные системы: ядро скопировано, у каждой копии много своей логики, и взвешенная оценка
    // низкая. Член ядра - не меньше CoreNodes узлов; форма, которая есть больше чем у MaxCoreTypes типов, - общая
    // обвязка (подпись, обработчик-заглушка), в ядро не идет
    public static class TypeCoreTool
    {
        // Наименьший член ядра: короче - свойство-литерал (Name => "...")
        public const int CoreNodes = 7;

        // Форма у большего числа типов - обвязка, а не скопированное ядро
        public const int MaxCoreTypes = 20;

        // Пары типов с общим ядром: число общих членов одной формы (с учетом повторов в типе)
        public static List<CorePair> Find(
            IReadOnlyList<TypeUnit> types,
            int minCoreMembers,
            Func<SimilarUnit, bool>? isProbe)
        {
            var result = new List<CorePair>();
            if (minCoreMembers <= 0)
                return result;

            var owners = new Dictionary<ulong, List<TypeUnit>>();
            foreach (var type in types)
            {
                foreach (var member in type.Members.Where(member => member.NodeCount >= CoreNodes))
                {
                    ulong key = GetShapeKey(member);
                    if (!owners.TryGetValue(key, out List<TypeUnit>? list))
                    {
                        list = new List<TypeUnit>();
                        owners[key] = list;
                    }

                    list.Add(type);
                }
            }

            var counts = new Dictionary<(TypeUnit Left, TypeUnit Right), int>();
            foreach (var list in owners.Values)
            {
                var distinct = list.Distinct().ToList();
                if (distinct.Count < 2 || distinct.Count > MaxCoreTypes)
                    continue;

                for (int i = 0; i < distinct.Count; i++)
                {
                    for (int j = i + 1; j < distinct.Count; j++)
                    {
                        var key = IsOrdered(distinct[i], distinct[j]) ? (distinct[i], distinct[j]) : (distinct[j], distinct[i]);
                        counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
                    }
                }
            }

            foreach (var pair in counts.Where(pair => pair.Value >= minCoreMembers))
            {
                bool isProbed = isProbe == null || isProbe(pair.Key.Left) || isProbe(pair.Key.Right);
                if (isProbed && !IsNested(pair.Key.Left, pair.Key.Right))
                    result.Add(new CorePair { Left = pair.Key.Left, Right = pair.Key.Right, SharedMembers = pair.Value });
            }

            return result;
        }

        // Хеш формы члена целиком: одинаковые отпечатки - одинаковый ключ
        private static ulong GetShapeKey(SimilarUnit member)
        {
            ulong hash = 1469598103934665603UL;
            foreach (ulong fingerprint in member.Fingerprints)
                hash = (hash ^ fingerprint) * 1099511628211UL;

            return hash;
        }

        private static bool IsOrdered(SimilarUnit left, SimilarUnit right)
        {
            return string.CompareOrdinal(left.File, right.File) < 0
                || left.File == right.File && left.SpanStart < right.SpanStart;
        }

        private static bool IsNested(SimilarUnit left, SimilarUnit right)
            => left.File == right.File && left.SpanStart < right.SpanEnd && right.SpanStart < left.SpanEnd;
    }
}
