using CSR.Core.Graph;

namespace CSR.Core.Similar
{
    // Система (L4): тип-корень и его окрестность в графе типов; роли узлов - путь видов ребер от корня
    public class SystemUnit : SimilarUnit
    {
        public required string RootKey { get; init; }

        public required HashSet<string> NodeKeys { get; init; }

        // Роль и типы в ней: "коллекция -> вариант: MoveForward, MoveBack"
        public required IReadOnlyList<string> RoleLines { get; init; }
    }

    // Единицы L4 по графу типов. Корень - хост: поле или коллекция точки расширения (абстрактный класс, интерфейс), у
    // которой не меньше MinVariants вариантов (система вариантов). Окрестность корня:
    // - от корня - поле, коллекция, возврат, new, виртуальный вызов, база, интерфейс;
    // - дальше, до глубины systemDepth, - только устройство системы: коллекция, возврат, new, виртуальный вызов
    //   (поля вариантов и их базы - частности варианта, они размывают окрестность);
    // - у точки расширения (абстрактный класс, интерфейс), которую система использует, - ее варианты (наследники и
    //   реализации); база и интерфейс самого типа - лист: через них не идем, иначе в окрестность попадают все
    //   соседи по базе;
    // - у самого корня - потребители (кто держит его полем, коллекцией, параметром, создает, вызывает).
    // Концентратор (больше HubDegree входящих ребер, например общая сущность) - лист: через него не идем.
    // Признак узла окрестности - его роль (путь видов ребер от корня) и обезличенная метка (вид, abstract, sealed,
    // обобщенность): "коллекция -> вариант -> возврат: структура". Отпечатки - хеши признаков без повторов, чтобы
    // число вариантов не решало. Хеши Вейсфейлера - Лемана по окрестности целиком оказались слишком точными: любая
    // разница в соседях давала новую метку, и системы одного устройства не находились
    public static class SystemUnits
    {
        public const int HubDegree = 40;

        // Наименьшее число вариантов точки расширения у хоста
        public const int MinVariants = 2;

        private static readonly EdgeKind[] FromRoot =
        [
            EdgeKind.Field,
            EdgeKind.Collection,
            EdgeKind.Returns,
            EdgeKind.Creates,
            EdgeKind.CallsVirtual,
            EdgeKind.Inherits,
            EdgeKind.Implements,
        ];

        private static readonly EdgeKind[] Structural =
        [
            EdgeKind.Collection,
            EdgeKind.Returns,
            EdgeKind.Creates,
            EdgeKind.CallsVirtual,
        ];

        private static readonly EdgeKind[] Consumers =
        [
            EdgeKind.Field,
            EdgeKind.Collection,
            EdgeKind.Parameter,
            EdgeKind.Creates,
            EdgeKind.CallsVirtual,
        ];

        public static List<SimilarUnit> Collect(TypeGraph graph, SimilarOptions options)
        {
            var units = new List<SimilarUnit>();
            foreach (var root in graph.Nodes.Where(IsHost))
            {
                var unit = Create(root, options.SystemDepth);
                if (unit.NodeCount >= options.MinSystemTypes)
                    units.Add(unit);
            }

            return units;
        }

        // Хост: класс, структура или запись с полем или коллекцией точки расширения, у которой есть варианты
        private static bool IsHost(TypeNode node)
        {
            if (node.Kind is not (TypeNodeKind.Class or TypeNodeKind.Struct or TypeNodeKind.Record))
                return false;

            return node.GetOut(EdgeKind.Field, EdgeKind.Collection)
                .Any(edge => edge.To.IsExtensionPoint
                    && edge.To.GetIn(EdgeKind.Inherits, EdgeKind.Implements).Count() >= MinVariants);
        }

        private static SystemUnit Create(TypeNode root, int depth)
        {
            var roles = GetRoles(root, depth);
            var nodes = new HashSet<TypeNode>(roles.Keys);
            var features = roles.Where(pair => pair.Key != root)
                .Select(pair => pair.Value + ": " + GetLabel(pair.Key))
                .Distinct()
                .ToList();
            var fingerprints = features.Select(ShapeHasher.HashText).Distinct().ToArray();
            Array.Sort(fingerprints);
            var roleLines = roles
                .Where(pair => pair.Key != root)
                .GroupBy(pair => pair.Value)
                .Select(group => $"{group.Key}: {string.Join(", ", group.Select(pair => pair.Key.Name).Distinct())}")
                .ToList();
            return new SystemUnit
            {
                Kind = UnitKind.System,
                File = root.File,
                Member = root.Name,
                StartLine = root.StartLine,
                EndLine = root.EndLine,
                NodeCount = nodes.Count,
                Fingerprints = fingerprints,
                SpanStart = root.StartLine,
                SpanEnd = root.EndLine + 1,
                Elements = features,
                RootKey = root.Key,
                NodeKeys = new HashSet<string>(nodes.Select(node => node.Key)),
                RoleLines = roleLines,
            };
        }

        // Узлы окрестности и их роли (путь от корня в порядке обхода в ширину)
        private static Dictionary<TypeNode, string> GetRoles(TypeNode root, int depth)
        {
            var roles = new Dictionary<TypeNode, string> { [root] = "" };
            var distances = new Dictionary<TypeNode, int> { [root] = 0 };
            var leaves = new HashSet<TypeNode>();
            var queue = new Queue<TypeNode>();
            queue.Enqueue(root);
            foreach (var edge in root.GetIn(Consumers).Where(edge => !roles.ContainsKey(edge.From)))
            {
                roles[edge.From] = "потребитель";
                distances[edge.From] = depth;
            }

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                int distance = distances[node];
                if (distance == depth || leaves.Contains(node) || node != root && node.In.Count > HubDegree)
                    continue;

                foreach (var step in GetSteps(node, node == root))
                {
                    if (roles.ContainsKey(step.Node))
                        continue;

                    var prefix = roles[node];
                    roles[step.Node] = prefix.Length == 0 ? step.Role : prefix + " -> " + step.Role;
                    distances[step.Node] = distance + 1;
                    if (step.IsLeaf)
                        leaves.Add(step.Node);

                    queue.Enqueue(step.Node);
                }
            }

            return roles;
        }

        private static IEnumerable<Step> GetSteps(TypeNode node, bool isRoot)
        {
            foreach (var edge in node.GetOut(isRoot ? FromRoot : Structural))
                yield return new Step(edge.To, GetRole(edge.Kind), edge.Kind is EdgeKind.Inherits or EdgeKind.Implements);

            if (!node.IsExtensionPoint)
                yield break;

            foreach (var edge in node.GetIn(EdgeKind.Inherits, EdgeKind.Implements))
                yield return new Step(edge.From, "вариант", false);
        }

        // Метка узла без имени: "абстрактный класс", "обобщенная структура"
        private static string GetLabel(TypeNode node)
        {
            var kind = node.Kind switch
            {
                TypeNodeKind.Struct => "структура",
                TypeNodeKind.Interface => "интерфейс",
                TypeNodeKind.Enum => "перечисление",
                TypeNodeKind.Record => "запись",
                TypeNodeKind.Delegate => "делегат",
                _ => "класс",
            };
            var parts = new List<string>();
            if (node.IsAbstract)
                parts.Add("абстрактный");

            if (node.IsSealed)
                parts.Add("sealed");

            if (node.IsGeneric)
                parts.Add("обобщенный");

            parts.Add(kind);
            return string.Join(" ", parts);
        }

        private static string GetRole(EdgeKind kind)
        {
            return kind switch
            {
                EdgeKind.Field => "поле",
                EdgeKind.Collection => "коллекция",
                EdgeKind.Returns => "возврат",
                EdgeKind.Creates => "создает",
                EdgeKind.CallsVirtual => "вызов",
                EdgeKind.Inherits => "база",
                EdgeKind.Implements => "интерфейс",
                _ => "параметр",
            };
        }

        private class Step
        {
            public Step(TypeNode node, string role, bool isLeaf)
            {
                Node = node;
                Role = role;
                IsLeaf = isLeaf;
            }

            public TypeNode Node { get; }

            public string Role { get; }

            // База или интерфейс: в окрестность входит, но не раскрывается
            public bool IsLeaf { get; }
        }
    }
}
