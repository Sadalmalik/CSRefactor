using System.Diagnostics;
using System.Text;
using CSR.Core.Checks;
using CSR.Reports.Style;

namespace CSR
{
    // Измененные строки по git: git diff -U0 от ревизии или готовый файл диффа; неотслеживаемые файлы - целиком.
    // Ключ результата - полный путь файла
    public static class GitDiff
    {
        public static Dictionary<string, ChangedLines> Collect(string source, string anyPath)
        {
            var folder = Directory.Exists(anyPath) ? anyPath : Path.GetDirectoryName(Path.GetFullPath(anyPath))!;
            var root = Run(folder, "rev-parse", "--show-toplevel").Trim();
            if (root.Length == 0)
                throw new UsageException($"{folder} - не репозиторий git");

            var diff = File.Exists(source)
                ? File.ReadAllText(source)
                : Run(root, "diff", "-U0", "--no-color", "--no-ext-diff", source);

            var result = new Dictionary<string, ChangedLines>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in UnifiedDiff.Parse(diff))
                result[ToFullPath(root, pair.Key)] = pair.Value;

            if (!File.Exists(source))
                AddUntracked(root, result);

            return result;
        }

        private static void AddUntracked(string root, Dictionary<string, ChangedLines> result)
        {
            var output = Run(root, "ls-files", "--others", "--exclude-standard");
            foreach (var line in output.Split('\n'))
            {
                var path = line.Trim();
                if (path.Length > 0)
                    result[ToFullPath(root, path)] = new ChangedLines { IsWholeFile = true };
            }
        }

        private static string ToFullPath(string root, string relative)
            => Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

        private static string Run(string folder, params string[] arguments)
        {
            var info = new ProcessStartInfo("git")
            {
                WorkingDirectory = folder,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            info.ArgumentList.Add("-c");
            info.ArgumentList.Add("core.quotepath=off");
            foreach (var argument in arguments)
                info.ArgumentList.Add(argument);

            using var process = Process.Start(info) ?? throw new UsageException("не удалось запустить git");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new UsageException($"git {string.Join(" ", arguments)}: {error.Trim()}");

            return output.Result;
        }
    }
}
