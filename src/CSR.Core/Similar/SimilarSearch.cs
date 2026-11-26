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

        // L2, L3: элементы, общие для всех мест кластера, и свои элементы каждого места
        public IReadOnlyList<string> Common { get; init; } = Array.Empty<string>();

        public IReadOnlyDictionary<SimilarUnit, IReadOnlyList<string>> Differences { get; init; } =
            new Dictionary<SimilarUnit, IReadOnlyList<string>>();

        // L3: тела членов места - сколько той же формы, что у пары, и какие со своей логикой; L4: роли и типы в них
        public IReadOnlyDictionary<SimilarUnit, IReadOnlyList<string>> Bodies { get; init; } =
            new Dictionary<SimilarUnit, IReadOnlyList<string>>();
    }

    public class SimilarResult
    {
        public required IReadOnlyList<SimilarPair> Pairs { get; init; }

        public required IReadOnlyList<SimilarCluster> Clusters { get; init; }
    }

    // Поиск целиком: пары по видам (окна фрагментов подряд склеиваются в один фрагмент; фрагменты членов,
    // которые уже похожи целиком, не повторяются), кластеры с общей частью и отличиями (L2, L3)
    public static class SimilarSearch
    {
        public static SimilarResult Run(
            IReadOnlyList<SimilarUnit> units,
            SimilarOptions options,
            Func<SimilarUnit, bool>? isProbe = null)
        {
            var found = SimilarFinder.Find(units, kind => UnitComparer.Create(kind, options), isProbe);
            var members = found.Where(pair => pair.Left.Kind == UnitKind.Member).ToList();
            var pairedMembers = members.ToLookup(pair => pair.Left, pair => pair.Right);
            var fragments = found
                .Where(pair => pair.Left.Kind == UnitKind.Fragment && !IsInPairedMembers(pair, pairedMembers))
                .ToList();

            var sets = found.Where(pair => pair.Left.Kind is UnitKind.ParameterSet or UnitKind.FieldSet
                or UnitKind.System);
            var types = AddCores(units, found.Where(pair => pair.Left.Kind == UnitKind.Type), options, isProbe);
            var pairs = members.Concat(MergeWindows(fragments, options.FragmentStatements))
                .Concat(sets)
                .Concat(types)
                .Select(pair => Orient(pair, isProbe))
                .OrderByDescending(pair => pair.Score)
                .ThenBy(pair => pair.Left.File, StringComparer.Ordinal)
                .ThenBy(pair => pair.Left.StartLine)
                .ToList();
            return new SimilarResult { Pairs = pairs, Clusters = BuildClusters(pairs) };
        }

        // Пары типов: по оценке и по общему ядру (TypeCoreTool); у каждой пары - число членов одной формы
        private static List<SimilarPair> AddCores(
            IReadOnlyList<SimilarUnit> units,
            IEnumerable<SimilarPair> scored,
            SimilarOptions options,
            Func<SimilarUnit, bool>? isProbe)
        {
            var comparer = new TypeComparer(options.TypeThreshold, options.Weights);
            var pairs = new List<SimilarPair>();
            var byLeft = new Dictionary<SimilarUnit, HashSet<SimilarUnit>>();
            foreach (var pair in scored)
            {
                pairs.Add(WithCore(pair, (TypeUnit)pair.Left, (TypeUnit)pair.Right));
                if (!byLeft.TryGetValue(pair.Left, out HashSet<SimilarUnit>? rights))
                {
                    rights = new HashSet<SimilarUnit>();
                    byLeft[pair.Left] = rights;
                }

                rights.Add(pair.Right);
            }

            var types = units.OfType<TypeUnit>().ToList();
            foreach (var core in TypeCoreTool.Find(types, options.MinCoreMembers, isProbe))
            {
                if (byLeft.TryGetValue(core.Left, out HashSet<SimilarUnit>? rights) && rights.Contains(core.Right))
                    continue;

                pairs.Add(new SimilarPair
                {
                    Left = core.Left,
                    Right = core.Right,
                    Score = comparer.GetScore(core.Left, core.Right),
                    SharedMembers = CountCore(core.Left, core.Right),
                });
            }

            return pairs;
        }

        private static SimilarPair WithCore(SimilarPair pair, TypeUnit left, TypeUnit right)
            => new() { Left = left, Right = right, Score = pair.Score, SharedMembers = CountCore(left, right) };

        // Члены ядра: сопоставлены с оценкой 1 и не короче TypeCoreTool.CoreNodes
        private static int CountCore(TypeUnit left, TypeUnit right)
        {
            return TypeComparer.Match(left, right)
                .Count(match => match.Score >= 1 && match.Left.NodeCount >= TypeCoreTool.CoreNodes);
        }

        // С отбором слева - проверяемая единица (измененный код), справа - похожая на нее
        private static SimilarPair Orient(SimilarPair pair, Func<SimilarUnit, bool>? isProbe)
        {
            if (isProbe == null || isProbe(pair.Left) || !isProbe(pair.Right))
                return pair;

            return new SimilarPair
            {
                Left = pair.Right,
                Right = pair.Left,
                Score = pair.Score,
                Windows = pair.Windows,
                SharedMembers = pair.SharedMembers,
            };
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

        // Кластеры по виду: единицы - сами по себе, фрагменты - по месту (перекрытые фрагменты файла - одно место)
        private static List<SimilarCluster> BuildClusters(List<SimilarPair> pairs)
        {
            var clusters = new List<SimilarCluster>();
            foreach (var group in pairs.GroupBy(pair => pair.Left.Kind))
            {
                var units = group.SelectMany(pair => new[] { pair.Left, pair.Right });
                var places = new PlaceSet(units, group.Key == UnitKind.Fragment);
                foreach (var pair in group)
                    places.Union(pair.Left, pair.Right);

                var components = places.GetComponents();
                foreach (var component in group.GroupBy(pair => places.Find(pair.Left)))
                {
                    var componentPairs = component.ToList();
                    var componentUnits = components[component.Key];
                    clusters.Add(new SimilarCluster
                    {
                        Kind = group.Key,
                        Units = componentUnits,
                        PairCount = componentPairs.Count,
                        MaxScore = componentPairs.Max(pair => pair.Score),
                        MinScore = componentPairs.Min(pair => pair.Score),
                        Common = ClusterSummary.GetCommon(componentUnits),
                        Differences = ClusterSummary.GetDifferences(componentUnits),
                        Bodies = ClusterSummary.GetBodies(componentUnits, componentPairs),
                    });
                }
            }

            return clusters
                .OrderByDescending(cluster => cluster.Units.Count)
                .ThenByDescending(cluster => cluster.MaxScore)
                .ThenByDescending(cluster => cluster.Units.Max(unit => unit.NodeCount))
                .ToList();
        }

        // Места кода и объединение их в компоненты (union-find). Для фрагментов перекрытые места файла - одно место
        private class PlaceSet
        {
            private readonly List<SimilarUnit> m_Places;
            private readonly Dictionary<SimilarUnit, int> m_Indexes = new();
            private readonly UnionFind m_Components;
            private readonly bool m_IsMergingOverlaps;

            public PlaceSet(IEnumerable<SimilarUnit> units, bool isMergingOverlaps)
            {
                m_IsMergingOverlaps = isMergingOverlaps;
                m_Places = units.Distinct()
                    .OrderBy(unit => unit.File, StringComparer.Ordinal)
                    .ThenBy(unit => unit.SpanStart)
                    .ToList();
                m_Components = new UnionFind(m_Places.Count);
                for (int i = 0; i < m_Places.Count; i++)
                    m_Indexes[m_Places[i]] = i;

                if (!isMergingOverlaps)
                    return;

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
                    if (m_IsMergingOverlaps && last != null && last.File == unit.File && unit.SpanStart < last.SpanEnd)
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
