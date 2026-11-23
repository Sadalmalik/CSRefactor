namespace CSR.Core.Similar
{
    // Группа похожих единиц одного вида: связная компонента графа пар
    public class SimilarCluster
    {
        public required UnitKind Kind { get; init; }

        public required IReadOnlyList<SimilarUnit> Units { get; init; }

        public required int PairCount { get; init; }

        public required double MaxScore { get; init; }

        public required double MinScore { get; init; }
    }

    public class SimilarResult
    {
        public required IReadOnlyList<SimilarPair> Pairs { get; init; }

        public required IReadOnlyList<SimilarCluster> Clusters { get; init; }
    }

    // Поиск целиком: пары членов, пары фрагментов (окна подряд склеиваются в один фрагмент; фрагменты членов,
    // которые уже похожи целиком, не повторяются), кластеры
    public static class SimilarSearch
    {
        public static SimilarResult Run(
            IReadOnlyList<SimilarUnit> units,
            SimilarOptions options,
            Func<SimilarUnit, bool>? isProbe = null)
        {
            var found = SimilarFinder.Find(units, options.Threshold, isProbe);
            var members = found.Where(pair => pair.Left.Kind == UnitKind.Member).ToList();
            var pairedMembers = members.ToLookup(pair => pair.Left, pair => pair.Right);
            var fragments = found
                .Where(pair => pair.Left.Kind == UnitKind.Fragment && !IsInPairedMembers(pair, pairedMembers))
                .ToList();

            var pairs = members.Concat(MergeWindows(fragments, options.FragmentStatements))
                .Select(pair => Orient(pair, isProbe))
                .OrderByDescending(pair => pair.Score)
                .ThenBy(pair => pair.Left.File, StringComparer.Ordinal)
                .ThenBy(pair => pair.Left.StartLine)
                .ToList();
            return new SimilarResult { Pairs = pairs, Clusters = BuildClusters(pairs) };
        }

        // С отбором слева - проверяемая единица (измененный код), справа - похожая на нее
        private static SimilarPair Orient(SimilarPair pair, Func<SimilarUnit, bool>? isProbe)
        {
            if (isProbe == null || isProbe(pair.Left) || !isProbe(pair.Right))
                return pair;

            return new SimilarPair { Left = pair.Right, Right = pair.Left, Score = pair.Score, Windows = pair.Windows };
        }

        private static bool IsInPairedMembers(SimilarPair pair, ILookup<SimilarUnit, SimilarUnit> pairedMembers)
        {
            var left = pair.Left.Owner;
            var right = pair.Right.Owner;
            if (left == null || right == null)
                return false;

            return pairedMembers[left].Contains(right) || pairedMembers[right].Contains(left);
        }

        // Окна одной диагонали (тот же блок слева и справа, тот же сдвиг номеров) с перекрытием или встык - один
        // фрагмент: начало первого окна, конец последнего, оценка - наибольшая
        private static List<SimilarPair> MergeWindows(List<SimilarPair> windows, int size)
        {
            var merged = new List<SimilarPair>();
            var diagonals = windows.GroupBy(pair => (pair.Left.File, pair.Left.BlockStart, pair.Right.File,
                pair.Right.BlockStart, pair.Right.StatementIndex - pair.Left.StatementIndex));
            foreach (var diagonal in diagonals)
            {
                var run = new List<SimilarPair>();
                foreach (var pair in diagonal.OrderBy(pair => pair.Left.StatementIndex))
                {
                    if (run.Count > 0 && pair.Left.StatementIndex > run[run.Count - 1].Left.StatementIndex + size)
                    {
                        merged.Add(Merge(run));
                        run.Clear();
                    }

                    run.Add(pair);
                }

                merged.Add(Merge(run));
            }

            return merged;
        }

        private static SimilarPair Merge(List<SimilarPair> run)
        {
            if (run.Count == 1)
                return run[0];

            return new SimilarPair
            {
                Left = Span(run.Select(pair => pair.Left).ToList()),
                Right = Span(run.Select(pair => pair.Right).ToList()),
                Score = run.Max(pair => pair.Score),
                Windows = run.Count,
            };
        }

        // Фрагмент от первого окна до последнего
        private static SimilarUnit Span(List<SimilarUnit> windows)
        {
            var first = windows[0];
            var last = windows[windows.Count - 1];
            return new SimilarUnit
            {
                Kind = UnitKind.Fragment,
                File = first.File,
                Member = first.Member,
                StartLine = first.StartLine,
                EndLine = last.EndLine,
                NodeCount = windows.Max(window => window.NodeCount),
                Fingerprints = first.Fingerprints,
                SpanStart = first.SpanStart,
                SpanEnd = last.SpanEnd,
                BlockStart = first.BlockStart,
                StatementIndex = first.StatementIndex,
                Owner = first.Owner,
            };
        }

        // Кластеры по виду: члены - по самой единице, фрагменты - по месту (перекрытые фрагменты файла - одно место)
        private static List<SimilarCluster> BuildClusters(List<SimilarPair> pairs)
        {
            var clusters = new List<SimilarCluster>();
            foreach (var group in pairs.GroupBy(pair => pair.Left.Kind))
            {
                var places = new PlaceSet(group.SelectMany(pair => new[] { pair.Left, pair.Right }));
                foreach (var pair in group)
                    places.Union(pair.Left, pair.Right);

                var components = places.GetComponents();
                foreach (var component in group.GroupBy(pair => places.Find(pair.Left)))
                {
                    var componentPairs = component.ToList();
                    clusters.Add(new SimilarCluster
                    {
                        Kind = group.Key,
                        Units = components[component.Key],
                        PairCount = componentPairs.Count,
                        MaxScore = componentPairs.Max(pair => pair.Score),
                        MinScore = componentPairs.Min(pair => pair.Score),
                    });
                }
            }

            return clusters
                .OrderByDescending(cluster => cluster.Units.Count)
                .ThenByDescending(cluster => cluster.MaxScore)
                .ThenByDescending(cluster => cluster.Units.Max(unit => unit.NodeCount))
                .ToList();
        }

        // Места кода и объединение их в компоненты (union-find). Перекрытые места одного файла - одно место
        private class PlaceSet
        {
            private readonly List<SimilarUnit> m_Places;
            private readonly Dictionary<SimilarUnit, int> m_Indexes = new();
            private readonly UnionFind m_Components;

            public PlaceSet(IEnumerable<SimilarUnit> units)
            {
                m_Places = units.Distinct()
                    .OrderBy(unit => unit.File, StringComparer.Ordinal)
                    .ThenBy(unit => unit.SpanStart)
                    .ToList();
                m_Components = new UnionFind(m_Places.Count);
                for (int i = 0; i < m_Places.Count; i++)
                    m_Indexes[m_Places[i]] = i;

                // группа перекрытых мест: открыта, пока следующее место начинается до наибольшего конца группы
                int open = 0;
                int openEnd = m_Places.Count > 0 ? m_Places[0].SpanEnd : 0;
                for (int i = 1; i < m_Places.Count; i++)
                {
                    var current = m_Places[i];
                    if (current.File == m_Places[open].File && current.SpanStart < openEnd)
                    {
                        m_Components.Union(open, i);
                        openEnd = Math.Max(openEnd, current.SpanEnd);
                        continue;
                    }

                    open = i;
                    openEnd = current.SpanEnd;
                }
            }

            public void Union(SimilarUnit left, SimilarUnit right)
                => m_Components.Union(m_Indexes[left], m_Indexes[right]);

            public int Find(SimilarUnit unit)
                => m_Components.Find(m_Indexes[unit]);

            // Места каждой компоненты по корню; перекрытые места одного файла склеены в одно
            public Dictionary<int, List<SimilarUnit>> GetComponents()
            {
                var components = new Dictionary<int, List<SimilarUnit>>();
                for (int i = 0; i < m_Places.Count; i++)
                {
                    int root = m_Components.Find(i);
                    if (!components.TryGetValue(root, out List<SimilarUnit>? places))
                    {
                        places = new List<SimilarUnit>();
                        components[root] = places;
                    }

                    var unit = m_Places[i];
                    var last = places.Count > 0 ? places[places.Count - 1] : null;
                    if (last != null && last.File == unit.File && unit.SpanStart < last.SpanEnd)
                        places[places.Count - 1] = Join(last, unit);
                    else
                        places.Add(unit);
                }

                return components;
            }

            private static SimilarUnit Join(SimilarUnit first, SimilarUnit second)
            {
                if (second.SpanEnd <= first.SpanEnd)
                    return first;

                return new SimilarUnit
                {
                    Kind = first.Kind,
                    File = first.File,
                    Member = first.Member,
                    StartLine = first.StartLine,
                    EndLine = second.EndLine,
                    NodeCount = Math.Max(first.NodeCount, second.NodeCount),
                    Fingerprints = first.Fingerprints,
                    SpanStart = first.SpanStart,
                    SpanEnd = second.SpanEnd,
                    Owner = first.Owner,
                };
            }
        }
    }
}
