using CSR.Core.Graph;

namespace CSR.Core.Similar
{
    // Система (L4): хост точек расширения с их вариантами и результатами
    public class SystemUnit : SimilarUnit
    {
        public required string RootKey { get; init; }

        // Типы системы: хост, базы вариантов, варианты, результаты
        public required HashSet<string> NodeKeys { get; init; }

        // Роль и типы в ней: "варианты (4): MotorArrow, MotorOrbit"
        public required IReadOnlyList<string> RoleLines { get; init; }
    }

    // Единицы L4 по графу типов: хост - тип, который держит полем или коллекцией точку расширения (абстрактный класс,
    // интерфейс) с не меньше MinVariants вариантами (ExtensionHosts). Признаки системы - устройство образца
    // "хост -> база вариантов -> варианты -> результат":
    // - хост и база - обезличенная метка (вид, abstract, sealed, обобщенность), поле или коллекция, число вариантов
    //   (ступени: 2-3, 4-9, 10 и больше);
    // - база - формы ее абстрактных, виртуальных и интерфейсных членов целиком и по частям ("возврат структура",
    //   "параметр float");
    // - результат - метка и типы полей ("поле Vector3").
    // Типы библиотек - по имени, типы исходного кода - по виду (R77). Без форм и типов полей устройство "хост -> список
    // вариантов -> результат" есть в проекте сотни раз, и системы одного назначения не отличаются от остальных.
    // Хеши Вейсфейлера - Лемана по окрестности оказались слишком точными: любая разница в соседях давала новую метку.
    // Точка расширения с больше чем MaxHosts хостами - инфраструктура (вычислители, действия, условия, которые держат
    // полем сотни типов): в инвентаре она есть, в сравнение не идет - иначе ее хосты совпадают друг с другом целиком
    public static class SystemUnits
    {
        public const int MinVariants = 2;

        public const int MaxHosts = 5;

        // Наименьшее число признаков системы: у меньших (одна база с двумя вариантами без результата) признаки
        // общие для всех и пары совпадают целиком
        public const int MinFeatures = 8;

        private const int ShownNames = 8;

        public static List<SimilarUnit> Collect(TypeGraph graph, SimilarOptions options)
        {
            var units = new List<SimilarUnit>();
            var hosts = ExtensionHosts.Find(graph, MinVariants);
            var hostCounts = hosts.GroupBy(entry => entry.ExtensionPoint).ToDictionary(
                group => group.Key,
                group => group.Count());
            var systems = hosts.Where(entry => hostCounts[entry.ExtensionPoint] <= MaxHosts);
            foreach (var group in systems.GroupBy(entry => entry.Host))
            {
                var unit = Create(group.Key, group.ToList());
                if (unit.NodeCount >= options.MinSystemTypes && unit.Fingerprints.Length >= MinFeatures)
                    units.Add(unit);
            }

            return units;
        }

        private static SystemUnit Create(TypeNode host, List<ExtensionHost> entries)
        {
            var features = new List<string> { "хост: " + GetLabel(host) };
            var nodes = new HashSet<TypeNode> { host };
            var roleLines = new List<string>();
            foreach (var entry in entries)
            {
                features.AddRange(GetFeatures(entry));
                nodes.Add(entry.ExtensionPoint);
                nodes.UnionWith(entry.Variants);
                nodes.UnionWith(entry.Results);
                var holder = entry.IsCollection ? "коллекция" : "поле";
                roleLines.Add($"база вариантов ({holder}): {entry.ExtensionPoint.Name}");
                roleLines.Add($"варианты ({entry.Variants.Count}): {JoinNames(entry.Variants)}");
                if (entry.Results.Count > 0)
                    roleLines.Add("результат: " + JoinNames(entry.Results));
            }

            var distinct = features.Distinct().ToList();
            var fingerprints = distinct.Select(ShapeHasher.HashText).Distinct().ToArray();
            Array.Sort(fingerprints);
            return new SystemUnit
            {
                Kind = UnitKind.System,
                File = host.File,
                Member = host.Name,
                StartLine = host.StartLine,
                EndLine = host.EndLine,
                NodeCount = nodes.Count,
                Fingerprints = fingerprints,
                SpanStart = host.StartLine,
                SpanEnd = host.EndLine + 1,
                Elements = distinct,
                RootKey = host.Key,
                NodeKeys = new HashSet<string>(nodes.Select(node => node.Key)),
                RoleLines = roleLines,
            };
        }

        private static IEnumerable<string> GetFeatures(ExtensionHost entry)
        {
            var point = entry.ExtensionPoint;
            yield return "база: " + GetLabel(point);
            yield return "база: " + (entry.IsCollection ? "в коллекции" : "в поле");
            yield return "база: вариантов " + GetVariantStep(entry.Variants.Count);
            foreach (var signature in point.ExtensionSignatures)
            {
                yield return "база: " + signature;
                foreach (var part in SplitSignature(signature))
                    yield return "база: " + part;
            }

            foreach (var result in entry.Results)
            {
                yield return "результат: " + GetLabel(result);
                foreach (var fieldType in result.FieldTypes)
                    yield return "результат: поле " + fieldType;
            }
        }

        // Имена через запятую, не больше ShownNames
        private static string JoinNames(IReadOnlyList<TypeNode> nodes)
        {
            var names = string.Join(", ", nodes.Take(ShownNames).Select(node => node.Name));
            return nodes.Count > ShownNames ? $"{names} и еще {nodes.Count - ShownNames}" : names;
        }

        // Число вариантов по ступеням: 2-3, 4-9, 10 и больше
        private static string GetVariantStep(int count)
        {
            return count switch
            {
                <= 3 => "2-3",
                <= 9 => "4-9",
                _ => "10+",
            };
        }

        // Части формы члена "метод RET(A, B)": "возврат RET", "параметр A", "параметр B"
        private static IEnumerable<string> SplitSignature(string signature)
        {
            int open = signature.IndexOf('(');
            int space = signature.IndexOf(' ');
            if (open < 0 || space < 0 || space > open)
                yield break;

            yield return "возврат " + signature.Substring(space + 1, open - space - 1);
            var arguments = signature.Substring(open + 1, signature.Length - open - 2);
            int depth = 0;
            int start = 0;
            for (int i = 0; i <= arguments.Length; i++)
            {
                if (i < arguments.Length)
                {
                    char symbol = arguments[i];
                    depth += symbol == '<'
                        ? 1
                        : symbol == '>'
                            ? -1
                            : 0;
                    if (symbol != ',' || depth > 0)
                        continue;
                }

                var argument = arguments.Substring(start, i - start).Trim();
                if (argument.Length > 0)
                    yield return "параметр " + argument;

                start = i + 1;
            }
        }

        // Метка узла без имени: "абстрактный класс", "обобщенный класс"
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
    }
}
