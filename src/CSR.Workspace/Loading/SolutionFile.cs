using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CSR.Workspace.Loading
{
    // Файл решения (.sln, .slnx): пути проектов и теневая копия с абсолютными путями проектов
    public static class SolutionFile
    {
        private static readonly Regex SlnProject = new(
            @"^(Project\(""\{[^}]+\}""\)\s*=\s*""[^""]*"",\s*"")([^""]+)("")",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        public static bool IsSolution(string path)
            => path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);

        // Абсолютные пути проектов решения (папки решения пропускаются)
        public static List<string> ReadProjects(string solutionPath)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(solutionPath))!;
            var projects = new List<string>();
            foreach (var relative in ReadProjectEntries(solutionPath))
                projects.Add(Path.GetFullPath(Path.Combine(folder, relative)));

            return projects;
        }

        // Копия решения в folder; пути проектов в копии абсолютные, сами проекты не копируются
        public static string WriteShadow(string solutionPath, string folder)
        {
            var source = Path.GetFullPath(solutionPath);
            var sourceFolder = Path.GetDirectoryName(source)!;
            Directory.CreateDirectory(folder);
            var shadow = Path.Combine(folder, Path.GetFileName(source));
            if (source.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                var document = XDocument.Load(source);
                foreach (var project in document.Descendants("Project"))
                {
                    var attribute = project.Attribute("Path");
                    if (attribute != null)
                        attribute.Value = Path.GetFullPath(Path.Combine(sourceFolder, ToLocal(attribute.Value)));
                }

                document.Save(shadow);
                return shadow;
            }

            var text = File.ReadAllText(source);
            var rewritten = SlnProject.Replace(text, match => IsProjectFile(match.Groups[2].Value)
                ? match.Groups[1].Value
                    + Path.GetFullPath(Path.Combine(sourceFolder, ToLocal(match.Groups[2].Value)))
                    + match.Groups[3].Value
                : match.Value);
            File.WriteAllText(shadow, rewritten);
            return shadow;
        }

        // Проект старого формата (без атрибута Sdk): так генерирует Unity
        public static bool IsLegacyProject(string projectPath)
        {
            if (!File.Exists(projectPath))
                return false;

            using var reader = new StreamReader(projectPath);
            var buffer = new char[4096];
            var length = reader.ReadBlock(buffer, 0, buffer.Length);
            var head = new string(buffer, 0, length);
            var start = head.IndexOf("<Project", StringComparison.Ordinal);
            if (start < 0)
                return false;

            var end = head.IndexOf('>', start);
            var element = end < 0 ? head.Substring(start) : head.Substring(start, end - start);
            return !element.Contains("Sdk=", StringComparison.Ordinal);
        }

        private static IEnumerable<string> ReadProjectEntries(string solutionPath)
        {
            if (solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                return XDocument.Load(solutionPath).Descendants("Project")
                    .Select(project => project.Attribute("Path")?.Value)
                    .Where(path => path != null)
                    .Select(path => ToLocal(path!))
                    .ToList();
            }

            return SlnProject.Matches(File.ReadAllText(solutionPath))
                .Select(match => match.Groups[2].Value)
                .Where(IsProjectFile)
                .Select(ToLocal)
                .ToList();
        }

        private static bool IsProjectFile(string path)
            => path.EndsWith("proj", StringComparison.OrdinalIgnoreCase);

        private static string ToLocal(string path)
            => path.Replace('\\',Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }
}
