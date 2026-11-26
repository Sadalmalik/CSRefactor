using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CSR.Core.Config;
using CSR.Core.Graph;
using CSR.Core.Structure;
using CSR.Core.Text;

namespace CSR.Reports.Structure
{
    // Отчет csr structure: метрики и отметки по порогам - вход аудитора, не находки
    public class StructureReport
    {
        public required int FileCount { get; init; }

        public required StructureOptions Options { get; init; }

        public required StructureResult Result { get; init; }

        public IEnumerable<OwnershipChain> GetDeepOwnership()
            => Result.Ownership.Where(chain => chain.Depth > Options.OwnershipDepth);

        public IEnumerable<TypeMetrics> GetDeepInheritance()
        {
            return Result.Types.Where(type => type.InheritanceDepth > Options.InheritanceDepth)
                .OrderByDescending(type => type.InheritanceDepth);
        }

        public IEnumerable<PackageMetrics> GetDumps(IReadOnlyList<PackageMetrics> packages)
        {
            return packages.Where(package => StructureResult.IsDump(package, Options))
                .OrderByDescending(package => package.Types.Count);
        }

        public IEnumerable<AssemblyMetrics> GetSmallAssemblies()
        {
            return Result.Assemblies.Where(assembly => assembly.IsSmall(Options))
                .OrderBy(assembly => assembly.Lines);
        }

        public IEnumerable<LayerChain> GetDeepLayers()
            => Result.LayerChains.Where(chain => chain.IsDeep(Options));

        public static string FormatLayerChain(LayerChain chain)
            => string.Join(" -> ", chain.Steps.Select(step => string.Join(" + ", step)));

        public static string FormatShare(double value)
            => value.ToString("0.00", CultureInfo.InvariantCulture);

        public static string FormatChain(OwnershipChain chain)
        {
            var steps = chain.Steps.Select(step => $"{step.Host.Name} -> {step.ExtensionPoint.Name}");
            return string.Join(" | ", steps);
        }

        public static string FormatType(TypeNode node)
            => $"{node.Name} ({node.File}:{node.StartLine})";
    }

    // JSON для агентов: все отметки и топы, пакеты целиком
    public static class StructureJsonWriter
    {
        private static readonly JsonWriterOptions Options = new()
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string Write(StructureReport report)
        {
            var result = report.Result;
            var options = report.Options;
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, Options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("files", report.FileCount);
                writer.WriteNumber("types", result.Graph.Nodes.Count);
                writer.WriteNumber("edges", result.Graph.EdgeCount);

                writer.WriteStartObject("ownership");
                writer.WriteNumber("threshold", options.OwnershipDepth);
                writer.WriteStartArray("chains");
                foreach (var chain in result.Ownership.Where(chain => chain.Depth > options.OwnershipDepth)
                    .Concat(result.Ownership.Where(chain => chain.Depth <= options.OwnershipDepth).TakeTop(options.Top)))
                    WriteChain(writer, chain, options);
                writer.WriteEndArray();
                writer.WriteEndObject();

                writer.WriteStartArray("gods");
                foreach (var type in result.GetGods(options))
                    WriteType(writer, type, options);
                writer.WriteEndArray();

                writer.WriteStartArray("inheritance");
                foreach (var type in result.Types.OrderByDescending(type => type.InheritanceDepth).TakeTop(options.Top))
                    WriteType(writer, type, options);
                writer.WriteEndArray();

                writer.WriteStartArray("hubs");
                foreach (var type in result.Types.OrderByDescending(type => type.Dependents).TakeTop(options.Top))
                    WriteType(writer, type, options);
                writer.WriteEndArray();

                WritePackages(writer, "folders", result.Folders, options);
                WritePackages(writer, "namespaces", result.Namespaces, options);
                WriteCycles(writer, "folderCycles", result.FolderCycles);
                WriteCycles(writer, "namespaceCycles", result.NamespaceCycles);
                WriteAssemblies(writer, result.Assemblies, options);
                WriteLayers(writer, result.LayerChains, options);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteChain(Utf8JsonWriter writer, OwnershipChain chain, StructureOptions options)
        {
            writer.WriteStartObject();
            WriteNode(writer, "root", chain.Root);
            writer.WriteNumber("depth", chain.Depth);
            writer.WriteBoolean("deep", chain.Depth > options.OwnershipDepth);
            writer.WriteStartArray("steps");
            foreach (var step in chain.Steps)
            {
                writer.WriteStartObject();
                writer.WriteString("host", step.Host.Name);
                writer.WriteString("extensionPoint", step.ExtensionPoint.Name);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteType(Utf8JsonWriter writer, TypeMetrics type, StructureOptions options)
        {
            writer.WriteStartObject();
            WriteNode(writer, "type", type.Node);
            writer.WriteNumber("inheritanceDepth", type.InheritanceDepth);
            writer.WriteNumber("children", type.Children);
            writer.WriteNumber("coupling", type.Coupling);
            writer.WriteNumber("dependents", type.Dependents);
            if (type.Facts != null)
            {
                writer.WriteNumber("wmc", type.Facts.Wmc);
                writer.WriteNumber("atfd", type.Facts.Atfd);
                writer.WriteNumber("tcc", Math.Round(type.Facts.Tcc, 3));
                writer.WriteNumber("methods", type.Facts.MethodCount);
            }

            writer.WriteBoolean("god", type.IsGod(options));
            writer.WriteEndObject();
        }

        private static void WritePackages(
            Utf8JsonWriter writer,
            string name,
            IReadOnlyList<PackageMetrics> packages,
            StructureOptions options)
        {
            writer.WriteStartArray(name);
            foreach (var package in packages)
            {
                writer.WriteStartObject();
                writer.WriteString("name", package.Name);
                writer.WriteNumber("types", package.Types.Count);
                writer.WriteNumber("afferent", package.Afferent);
                writer.WriteNumber("efferent", package.Efferent);
                writer.WriteNumber("instability", Math.Round(package.Instability, 3));
                writer.WriteNumber("abstractness", Math.Round(package.Abstractness, 3));
                writer.WriteNumber("distance", Math.Round(package.Distance, 3));
                writer.WriteNumber("internalEdges", package.InternalEdges);
                writer.WriteNumber("externalEdges", package.ExternalEdges);
                writer.WriteNumber("communities", package.Communities);
                writer.WriteNumber("modularity", Math.Round(package.Modularity, 3));
                writer.WriteNumber("isolatedShare", Math.Round(package.IsolatedShare, 3));
                writer.WriteBoolean("dump", StructureResult.IsDump(package, options));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteCycles(Utf8JsonWriter writer, string name, IReadOnlyList<PackageCycle> cycles)
        {
            writer.WriteStartArray(name);
            foreach (var cycle in cycles)
            {
                writer.WriteStartObject();
                writer.WriteStartArray("packages");
                foreach (var package in cycle.Packages)
                    writer.WriteStringValue(package);
                writer.WriteEndArray();
                writer.WriteStartArray("links");
                foreach (var link in cycle.Links)
                {
                    writer.WriteStartObject();
                    writer.WriteString("from", link.From);
                    writer.WriteString("to", link.To);
                    writer.WriteNumber("count", link.Count);
                    writer.WriteNumber("reverse", link.Reverse);
                    writer.WriteString("example", $"{link.Example.From.Name} -> {link.Example.To.Name} ({link.Example.Kind})");
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private static void WriteAssemblies(
            Utf8JsonWriter writer,
            IReadOnlyList<AssemblyMetrics> assemblies,
            StructureOptions options)
        {
            writer.WriteStartObject("assemblies");
            writer.WriteNumber("minLines", options.MinAssemblyLines);
            writer.WriteStartArray("items");
            foreach (var assembly in assemblies)
            {
                writer.WriteStartObject();
                writer.WriteString("name", assembly.Name);
                writer.WriteNumber("lines", assembly.Lines);
                writer.WriteNumber("types", assembly.Types);
                writer.WriteStartArray("dependencies");
                foreach (var dependency in assembly.Dependencies)
                    writer.WriteStringValue(dependency);
                writer.WriteEndArray();
                writer.WriteBoolean("small", assembly.IsSmall(options));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteLayers(Utf8JsonWriter writer, IReadOnlyList<LayerChain> chains, StructureOptions options)
        {
            writer.WriteStartObject("layers");
            writer.WriteNumber("threshold", options.LayerDepth);
            writer.WriteStartArray("items");
            foreach (var chain in chains)
            {
                writer.WriteStartObject();
                writer.WriteString("name", chain.Layer);
                writer.WriteNumber("depth", chain.Depth);
                writer.WriteBoolean("deep", chain.IsDeep(options));
                writer.WriteStartArray("chain");
                foreach (var step in chain.Steps)
                {
                    writer.WriteStartArray();
                    foreach (var assembly in step)
                        writer.WriteStringValue(assembly);
                    writer.WriteEndArray();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private static void WriteNode(Utf8JsonWriter writer, string name, TypeNode node)
        {
            writer.WriteStartObject(name);
            writer.WriteString("name", node.Name);
            writer.WriteString("file", node.File);
            writer.WriteNumber("startLine", node.StartLine);
            writer.WriteEndObject();
        }
    }

    // Отчет для человека: сводка, отметки по порогам, топы
    public static class StructureMarkdownWriter
    {
        // Сколько слабых связей цикла показывать
        private const int ShownLinks = 5;

        public static string Write(StructureReport report)
        {
            var result = report.Result;
            var options = report.Options;
            var deep = report.GetDeepOwnership().ToList();
            var gods = result.GetGods(options).ToList();
            var folderDumps = report.GetDumps(result.Folders).ToList();
            var text = new StringBuilder();
            text.AppendLine("# csr structure");
            text.AppendLine();
            text.AppendLine($"Файлов {report.FileCount}, типов {result.Graph.Nodes.Count}, ребер {result.Graph.EdgeCount}; "
                + $"папок {result.Folders.Count}, пространств {result.Namespaces.Count}.");
            text.AppendLine();
            text.AppendLine($"Отметки: владение глубже {options.OwnershipDepth} - {deep.Count}, "
                + $"наследование глубже {options.InheritanceDepth} - {report.GetDeepInheritance().Count()}, "
                + $"объектов-богов - {gods.Count}, "
                + $"свалок (папки) - {folderDumps.Count}, циклов папок - {result.FolderCycles.Count}, "
                + $"циклов пространств - {result.NamespaceCycles.Count}"
                + $"{FormatAssemblyMarks(report)}.");

            WriteOwnership(text, result.Ownership, options);
            WriteGods(text, gods, options);
            WriteCycles(text, "Циклы папок", result.FolderCycles, options.Top);
            WriteCycles(text, "Циклы пространств имен", result.NamespaceCycles, options.Top);
            WriteDumps(text, folderDumps, options.Top);
            WriteAssemblies(text, report);
            WriteLayers(text, report);
            WriteTypes(
                text,
                "Глубина наследования",
                result.Types.OrderByDescending(type => type.InheritanceDepth),
                type => $"глубина {type.InheritanceDepth}, наследников {type.Children}",
                options.Top);
            WriteTypes(
                text,
                "Концентраторы",
                result.Types.OrderByDescending(type => type.Dependents),
                type => $"зависят {type.Dependents}, связанных типов {type.Coupling}",
                options.Top);
            WritePackages(text, "Папки по расстоянию от главной последовательности", result.Folders, options.Top);
            return text.ToString();
        }

        private static void WriteOwnership(StringBuilder text, IReadOnlyList<OwnershipChain> chains, StructureOptions options)
        {
            text.AppendLine();
            text.AppendLine($"## Глубина владения (порог {options.OwnershipDepth})");
            text.AppendLine();
            foreach (var chain in chains.TakeTop(options.Top))
            {
                var mark = chain.Depth > options.OwnershipDepth ? " **глубже порога**" : "";
                text.AppendLine(
                    $"- {chain.Depth}{mark}: `{StructureReport.FormatType(chain.Root)}`: {StructureReport.FormatChain(chain)}");
            }
        }

        private static void WriteGods(StringBuilder text, List<TypeMetrics> gods, StructureOptions options)
        {
            text.AppendLine();
            text.AppendLine($"## Объекты-боги (WMC >= {options.GodWmc}, ATFD > {options.GodAtfd}, "
                + $"TCC < {StructureReport.FormatShare(options.GodTcc)}): {gods.Count}");
            text.AppendLine();
            foreach (var type in gods.TakeTop(options.Top))
            {
                var facts = type.Facts!;
                text.AppendLine($"- `{StructureReport.FormatType(type.Node)}`: WMC {facts.Wmc}, ATFD {facts.Atfd}, "
                    + $"TCC {StructureReport.FormatShare(facts.Tcc)}, методов {facts.MethodCount}, "
                    + $"связанных типов {type.Coupling}");
            }
        }

        private static void WriteCycles(StringBuilder text, string title, IReadOnlyList<PackageCycle> cycles, int top)
        {
            text.AppendLine();
            text.AppendLine($"## {title}: {cycles.Count}");
            foreach (var cycle in cycles.TakeTop(top))
            {
                text.AppendLine();
                var names = FindingTextTools.JoinNames(cycle.Packages.Select(name => $"`{name}`").ToList());
                text.AppendLine($"- пакетов {cycle.Packages.Count}: {names}");
                foreach (var link in cycle.Links.Take(ShownLinks))
                {
                    var reverse = link.Reverse > 0 ? $" (обратно {link.Reverse})" : "";
                    text.AppendLine($"  - `{link.From}` -> `{link.To}`: связей {link.Count}{reverse}, например "
                        + $"`{link.Example.From.Name}` -> `{link.Example.To.Name}` ({link.Example.Kind})");
                }
            }
        }

        private static void WriteDumps(StringBuilder text, List<PackageMetrics> dumps, int top)
        {
            text.AppendLine();
            text.AppendLine($"## Свалки (папки): {dumps.Count}");
            text.AppendLine();
            foreach (var package in dumps.TakeTop(top))
            {
                text.AppendLine($"- `{package.Name}`: типов {package.Types.Count}, сообществ {package.Communities}, "
                    + $"изолированных {StructureReport.FormatShare(package.IsolatedShare)}, "
                    + $"модулярность {StructureReport.FormatShare(package.Modularity)}, "
                    + $"внешних ребер {StructureReport.FormatShare(package.ExternalShare)}");
            }
        }

        // Отметки сборок и слоев - только при заданном пороге
        private static string FormatAssemblyMarks(StructureReport report)
        {
            var options = report.Options;
            var marks = new StringBuilder();
            if (options.MinAssemblyLines > 0)
                marks.Append($", сборок меньше {options.MinAssemblyLines} строк - {report.GetSmallAssemblies().Count()}");
            if (options.LayerDepth > 0)
                marks.Append($", слоев глубже {options.LayerDepth} - {report.GetDeepLayers().Count()}");
            return marks.ToString();
        }

        // Сборки по возрастанию размера; без сборок в цели - раздела нет
        private static void WriteAssemblies(StringBuilder text, StructureReport report)
        {
            var result = report.Result;
            var options = report.Options;
            if (result.Assemblies.Count == 0)
                return;

            text.AppendLine();
            var threshold = options.MinAssemblyLines > 0 ? $" (порог {options.MinAssemblyLines} строк)" : "";
            text.AppendLine($"## Сборки{threshold}: {result.Assemblies.Count}");
            text.AppendLine();
            foreach (var assembly in result.Assemblies.OrderBy(assembly => assembly.Lines).TakeTop(options.Top))
            {
                var mark = assembly.IsSmall(options) ? " **меньше порога**" : "";
                var dependencies = string.Join(", ", assembly.Dependencies.Select(name => $"`{name}`"));
                text.AppendLine($"- `{assembly.Name}`{mark}: строк {assembly.Lines}, типов {assembly.Types}, "
                    + $"зависит от {assembly.Dependencies.Count}{(dependencies.Length > 0 ? ": " : "")}{dependencies}");
            }
        }

        private static void WriteLayers(StringBuilder text, StructureReport report)
        {
            var options = report.Options;
            if (report.Result.LayerChains.Count == 0)
                return;

            text.AppendLine();
            var threshold = options.LayerDepth > 0 ? $" (порог {options.LayerDepth})" : "";
            text.AppendLine($"## Глубина зависимостей в слое{threshold}");
            text.AppendLine();
            foreach (var chain in report.Result.LayerChains)
            {
                var mark = chain.IsDeep(options) ? " **глубже порога**" : "";
                text.AppendLine($"- `{chain.Layer}`: {chain.Depth}{mark}: {StructureReport.FormatLayerChain(chain)}");
            }
        }

        private static void WriteTypes(
            StringBuilder text,
            string title,
            IEnumerable<TypeMetrics> types,
            Func<TypeMetrics, string> describe,
            int top)
        {
            text.AppendLine();
            text.AppendLine($"## {title}");
            text.AppendLine();
            foreach (var type in types.TakeTop(top))
                text.AppendLine($"- `{StructureReport.FormatType(type.Node)}`: {describe(type)}");
        }

        private static void WritePackages(StringBuilder text, string title, IReadOnlyList<PackageMetrics> packages, int top)
        {
            text.AppendLine();
            text.AppendLine($"## {title}");
            text.AppendLine();
            var ordered = packages.Where(package => package.Types.Count >= 5)
                .OrderByDescending(package => package.Distance)
                .ThenByDescending(package => package.Types.Count);
            foreach (var package in ordered.TakeTop(top))
            {
                text.AppendLine($"- `{package.Name}`: D {StructureReport.FormatShare(package.Distance)}, "
                    + $"I {StructureReport.FormatShare(package.Instability)}, "
                    + $"A {StructureReport.FormatShare(package.Abstractness)}, "
                    + $"Ca {package.Afferent}, Ce {package.Efferent}, типов {package.Types.Count}");
            }
        }
    }
}
