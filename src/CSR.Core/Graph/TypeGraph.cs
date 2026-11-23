using Microsoft.CodeAnalysis;

namespace CSR.Core.Graph
{
    public enum TypeNodeKind
    {
        Class,
        Struct,
        Interface,
        Enum,
        Record,
        Delegate,
    }

    // Вид связи от типа From к типу To
    public enum EdgeKind
    {
        // From наследует To (класс от класса, интерфейс от интерфейса)
        Inherits,

        // From реализует интерфейс To
        Implements,

        // поле или свойство типа To
        Field,

        // поле или свойство - коллекция элементов To (массив, List<To>, значение Dictionary<K, To>)
        Collection,

        // метод возвращает To (или коллекцию To)
        Returns,

        // параметр метода или конструктора типа To
        Parameter,

        // new To(...) в теле
        Creates,

        // вызов виртуального, абстрактного или интерфейсного члена To
        CallsVirtual,
    }

    // Тип исходного кода. Метки без имени (вид, abstract, sealed, обобщенность, поля) - для сравнения устройства;
    // имя, пространство, файл - для отчета и будущих метрик по папкам и пространствам
    public class TypeNode
    {
        private readonly List<TypeEdge> m_Out = new();
        private readonly List<TypeEdge> m_In = new();

        public required string Key { get; init; }

        public required string Name { get; init; }

        public required string Namespace { get; init; }

        public required string File { get; init; }

        public required int StartLine { get; init; }

        public required int EndLine { get; init; }

        public required TypeNodeKind Kind { get; init; }

        public required bool IsAbstract { get; init; }

        public required bool IsSealed { get; init; }

        public required bool IsGeneric { get; init; }

        public required int FieldCount { get; init; }

        // Вложенный тип: владелец (будущая метрика глубины вложенности)
        public TypeNode? Container { get; internal set; }

        public IReadOnlyList<TypeEdge> Out
            => m_Out;

        public IReadOnlyList<TypeEdge> In
            => m_In;

        // Абстрактный класс или интерфейс: точка расширения
        public bool IsExtensionPoint
            => Kind == TypeNodeKind.Interface || IsAbstract && Kind == TypeNodeKind.Class;

        public IEnumerable<TypeEdge> GetOut(params EdgeKind[] kinds)
            => kinds.Length == 0 ? m_Out : m_Out.Where(edge => kinds.Contains(edge.Kind));

        public IEnumerable<TypeEdge> GetIn(params EdgeKind[] kinds)
            => kinds.Length == 0 ? m_In : m_In.Where(edge => kinds.Contains(edge.Kind));

        internal void AddOut(TypeEdge edge)
            => m_Out.Add(edge);

        internal void AddIn(TypeEdge edge)
            => m_In.Add(edge);
    }

    // Связь между типами исходного кода; Count - сколько мест дали связь (полей, вызовов)
    public class TypeEdge
    {
        public required TypeNode From { get; init; }

        public required TypeNode To { get; init; }

        public required EdgeKind Kind { get; init; }

        public int Count { get; internal set; } = 1;
    }

    // Граф типов исходного кода: узлы - типы выбранных деревьев (partial - один узел), ребра - связи между ними.
    // Типы библиотек в граф не входят. Строится один раз (TypeGraphBuilder) и общий для потребителей: похожие
    // системы (csr similar, L4), структурные метрики (наследование, вложенность, папки, циклы)
    public class TypeGraph
    {
        private readonly Dictionary<string, TypeNode> m_Nodes;

        internal TypeGraph(Dictionary<string, TypeNode> nodes)
        {
            m_Nodes = nodes;
        }

        public IReadOnlyCollection<TypeNode> Nodes
            => m_Nodes.Values;

        public int EdgeCount
            => m_Nodes.Values.Sum(node => node.Out.Count);

        public TypeNode? Find(string key)
            => m_Nodes.TryGetValue(key, out TypeNode? node) ? node : null;

        public TypeNode? Find(ITypeSymbol symbol)
            => Find(GetKey(symbol));

        // Узлы подграфа (папка, пространство имен): ребра - те, у которых оба конца в подграфе
        public IReadOnlyList<TypeNode> Select(Func<TypeNode, bool> predicate)
            => m_Nodes.Values.Where(predicate).ToList();

        // Обход от узла по ребрам заданных видов (исходящим и, при isIncoming, входящим) на глубину depth
        public IReadOnlyDictionary<TypeNode, int> Walk(TypeNode start, int depth, bool isIncoming, params EdgeKind[] kinds)
        {
            var distances = new Dictionary<TypeNode, int> { [start] = 0 };
            var queue = new Queue<TypeNode>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                int distance = distances[node];
                if (distance == depth)
                    continue;

                var next = node.GetOut(kinds).Select(edge => edge.To);
                if (isIncoming)
                    next = next.Concat(node.GetIn(kinds).Select(edge => edge.From));

                foreach (var neighbour in next.Where(neighbour => !distances.ContainsKey(neighbour)))
                {
                    distances[neighbour] = distance + 1;
                    queue.Enqueue(neighbour);
                }
            }

            return distances;
        }

        // Ключ типа, общий для его частей partial и копий в разных компиляциях
        public static string GetKey(ITypeSymbol symbol)
            => symbol.OriginalDefinition.GetDocumentationCommentId() ?? symbol.OriginalDefinition.ToDisplayString();
    }
}
