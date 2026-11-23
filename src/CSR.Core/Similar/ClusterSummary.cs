namespace CSR.Core.Similar
{
    // Общая часть и отличия кластера L2 и L3. Общее - элементы, которые есть у всех мест (с учетом повторов);
    // отличия места - его остальные элементы. Тела типа: члены той же формы, что у пары (отличаются данными -
    // именами и литералами), и члены своей логики
    public static class ClusterSummary
    {
        public static IReadOnlyList<string> GetCommon(IReadOnlyList<SimilarUnit> units)
        {
            if (units.Count == 0)
                return Array.Empty<string>();

            var common = Count(units[0].Elements);
            foreach (var unit in units.Skip(1))
            {
                var counts = Count(unit.Elements);
                foreach (var element in common.Keys.ToList())
                    common[element] = counts.TryGetValue(element, out int count) ? Math.Min(common[element], count) : 0;
            }

            return Take(units[0].Elements, common);
        }

        public static IReadOnlyDictionary<SimilarUnit, IReadOnlyList<string>> GetDifferences(IReadOnlyList<SimilarUnit> units)
        {
            var differences = new Dictionary<SimilarUnit, IReadOnlyList<string>>();
            if (units.All(unit => unit.Elements.Count == 0))
                return differences;

            var common = Count(GetCommon(units));
            foreach (var unit in units)
            {
                var own = Count(unit.Elements);
                foreach (var pair in common)
                    own[pair.Key] = own.TryGetValue(pair.Key, out int count) ? count - pair.Value : 0;

                differences[unit] = Take(unit.Elements, own);
            }

            return differences;
        }

        public static IReadOnlyDictionary<SimilarUnit, IReadOnlyList<string>> GetBodies(
            IReadOnlyList<SimilarUnit> units,
            IReadOnlyList<SimilarPair> pairs)
        {
            var bodies = new Dictionary<SimilarUnit, IReadOnlyList<string>>();
            foreach (var system in units.OfType<SystemUnit>())
                bodies[system] = system.RoleLines;

            var types = units.OfType<TypeUnit>().Where(type => type.Members.Count > 0).ToList();
            if (types.Count == 0)
                return bodies;

            var sameShape = GetSameShapeMembers(pairs);
            foreach (var type in types)
                bodies[type] = DescribeBodies(type, sameShape).ToList();

            return bodies;
        }

        // Члены типов, которым в какой-нибудь паре кластера нашелся член той же формы (оценка 1)
        private static HashSet<SimilarUnit> GetSameShapeMembers(IReadOnlyList<SimilarPair> pairs)
        {
            var same = new HashSet<SimilarUnit>();
            foreach (var pair in pairs)
            {
                if (pair.Left is not TypeUnit left || pair.Right is not TypeUnit right)
                    continue;

                foreach (var match in TypeComparer.Match(left, right).Where(match => match.Score >= 1))
                {
                    same.Add(match.Left);
                    same.Add(match.Right);
                }
            }

            return same;
        }

        private static IEnumerable<string> DescribeBodies(TypeUnit type, HashSet<SimilarUnit> sameShape)
        {
            int same = type.Members.Count(sameShape.Contains);
            if (same > 0)
                yield return $"та же форма (отличия - имена и литералы): членов {same} из {type.Members.Count}";

            var own = type.Members
                .Where(member => !sameShape.Contains(member))
                .Select(member => GetShortName(type, member))
                .ToList();
            if (own.Count > 0)
                yield return "своя логика: " + string.Join(", ", own);
        }

        private static string GetShortName(TypeUnit type, SimilarUnit member)
        {
            var prefix = type.Member + ".";
            return member.Member.StartsWith(prefix, StringComparison.Ordinal)
                ? member.Member.Substring(prefix.Length)
                : member.Member;
        }

        private static Dictionary<string, int> Count(IEnumerable<string> elements)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var element in elements)
                counts[element] = counts.TryGetValue(element, out int count) ? count + 1 : 1;

            return counts;
        }

        // Элементы в порядке места, сколько раз разрешает счетчик
        private static IReadOnlyList<string> Take(IReadOnlyList<string> elements, Dictionary<string, int> counts)
        {
            var left = new Dictionary<string, int>(counts, StringComparer.Ordinal);
            var result = new List<string>();
            foreach (var element in elements)
            {
                if (!left.TryGetValue(element, out int count) || count <= 0)
                    continue;

                left[element] = count - 1;
                result.Add(element);
            }

            return result;
        }
    }
}
