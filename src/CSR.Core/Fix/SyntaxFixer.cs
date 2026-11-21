using CSR.Core.Config;
using CSR.Core.Checks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Fix
{
    public class FixOptions
    {
        // Замены находок этого уровня и выше: по умолчанию error и warning; hint - суждение
        // (coalesce-assignment путает объекты Unity, commented-code удаляет строки)
        public Severity MinSeverity { get; set; } = Severity.Warning;

        // Только эти проверки; null - все с заменой
        public ISet<string>? Checks { get; set; }

        public int MaxPasses { get; set; } = 8;
    }

    public class FixResult
    {
        public required string Text { get; init; }

        // Примененные находки; строки - в тексте того прохода, в котором замена применена
        public required IReadOnlyList<Finding> Applied { get; init; }

        // Находки исправленного текста (с учетом измененных строк)
        public required IReadOnlyList<Finding> Remaining { get; init; }

        public required int Passes { get; init; }

        // Замены дали новые синтаксические ошибки: файл оставлен как был (ошибка проверки - сообщить)
        public bool Reverted { get; init; }
    }

    // csr fix: замены находок (Finding.Replacement) применяются к тексту проходами. За проход - все замены,
    // которые не пересекаются по строкам; затем текст разбирается заново: несколько находок на одной строке
    // дают замены от одного исходного текста, следующая применяется к уже исправленному
    public static class SyntaxFixer
    {
        // changed: измененные строки (режим changed-lines); замены сдвигают и переносят их; null - весь файл
        public static FixResult Fix(
            string source,
            string path,
            CheckOptions options,
            ProjectOptions project,
            FixOptions fix,
            ChangedLines? changed)
        {
            var lines = SourceLines.Parse(source);
            var flags = GetFlags(changed, source);
            var applied = new List<Finding>();
            var seen = new HashSet<string> { source };
            var passes = 0;
            var findings = Analyze(lines.ToString(), path, options, project, flags);
            while (passes < fix.MaxPasses)
            {
                var batch = SelectBatch(findings, lines, fix);
                if (batch.Count == 0)
                    break;

                // снизу вверх: номера строк выше замены не меняются
                foreach (var finding in batch.OrderByDescending(item => item.Line))
                {
                    lines.Replace(finding.Line, finding.EndLine, finding.Replacement!);
                    if (flags != null)
                        ReplaceFlags(flags, finding);
                }

                passes++;
                applied.AddRange(batch);
                var text = lines.ToString();
                findings = Analyze(text, path, options, project, flags);
                if (!seen.Add(text))
                    break;
            }

            var result = lines.ToString();
            if (passes == 0 || CountSyntaxErrors(result, path, project) <= CountSyntaxErrors(source, path, project))
                return new FixResult { Text = result, Applied = applied, Remaining = findings, Passes = passes };

            return new FixResult
            {
                Text = source,
                Applied = [],
                Remaining = Analyze(source, path, options, project, GetFlags(changed, source)),
                Passes = passes,
                Reverted = true,
            };
        }

        private static int CountSyntaxErrors(string text, string path, ProjectOptions project)
        {
            var tree = CSharpSyntaxTree.ParseText(text, project.ParseOptions, path);
            return tree.GetDiagnostics().Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        }

        private static List<Finding> Analyze(
            string text,
            string path,
            CheckOptions options,
            ProjectOptions project,
            List<bool>? flags)
        {
            var tree = CSharpSyntaxTree.ParseText(text, project.ParseOptions, path);
            var findings = SyntaxAnalyzer.Analyze(tree, options, project);
            if (flags == null)
                return findings;

            var changed = new ChangedLines();
            for (var i = 0; i < flags.Count; i++)
            {
                if (flags[i])
                    changed.Add(i + 1, 1);
            }

            return findings.Where(changed.Intersects).ToList();
        }

        // Находки с заменой нужного уровня и проверки, без пересечения строк: первая по порядку строк
        private static List<Finding> SelectBatch(List<Finding> findings, SourceLines lines, FixOptions fix)
        {
            var batch = new List<Finding>();
            var lastLine = 0;
            var candidates = findings
                .Where(finding => finding.Replacement != null && finding.Severity >= fix.MinSeverity)
                .Where(finding => fix.Checks == null || fix.Checks.Contains(finding.Check))
                .OrderBy(finding => finding.Line)
                .ThenBy(finding => finding.Column);
            foreach (var finding in candidates)
            {
                if (finding.Line <= lastLine || IsSameText(finding, lines))
                    continue;

                batch.Add(finding);
                lastLine = finding.EndLine;
            }

            return batch;
        }

        // Замена, равная исходным строкам, ничего не исправляет и зациклила бы проходы
        private static bool IsSameText(Finding finding, SourceLines lines)
        {
            var replacement = finding.Replacement!;
            if (replacement.Count != finding.EndLine - finding.Line + 1)
                return false;

            for (var i = 0; i < replacement.Count; i++)
            {
                if (replacement[i] != lines[finding.Line + i])
                    return false;
            }

            return true;
        }

        // null - весь файл
        private static List<bool>? GetFlags(ChangedLines? changed, string source)
        {
            if (changed is null or { IsWholeFile: true })
                return null;

            var count = SourceLines.Parse(source).Count;
            var flags = new List<bool>(count);
            for (var line = 1; line <= count; line++)
                flags.Add(changed.Contains(line));

            return flags;
        }

        // Новые строки замены - измененные, если измененной была хоть одна заменяемая
        private static void ReplaceFlags(List<bool> flags, Finding finding)
        {
            var first = finding.Line - 1;
            var count = finding.EndLine - finding.Line + 1;
            var changed = flags.GetRange(first, count).Any(flag => flag);
            flags.RemoveRange(first, count);
            flags.InsertRange(first, Enumerable.Repeat(changed, finding.Replacement!.Count));
        }
    }
}
