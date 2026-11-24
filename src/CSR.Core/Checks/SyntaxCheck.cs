using CSR.Core.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CSR.Core.Checks
{
    // Проверка по синтаксису одного файла - общий движок режимов style и arch. Нейтральна: что считать нарушением,
    // задают параметры и конфиг; ID правил гайдов в движке нет
    public abstract class SyntaxCheck
    {
        public abstract string Name { get; }

        public abstract string Description { get; }

        public virtual IReadOnlyList<CheckParameter> Parameters
            => Array.Empty<CheckParameter>();

        // Устаревшая проверка: чем заменить (предупреждение при загрузке конфига); null - не устарела
        public virtual string? Deprecated
            => null;

        public abstract void Analyze(CheckContext context);
    }

    // Включенная проверка: уровень, метка правила и параметры
    public class CheckConfiguration
    {
        public required SyntaxCheck Check { get; init; }

        public required Severity Severity { get; set; }

        public string? Rule { get; set; }

        public required CheckSettings Settings { get; init; }

        // Конфигурация проверки с параметрами по умолчанию
        public static CheckConfiguration Create(SyntaxCheck check, Severity severity, string? rule = null)
        {
            return new CheckConfiguration
            {
                Check = check,
                Severity = severity,
                Rule = rule,
                Settings = new CheckSettings(check.Parameters),
            };
        }
    }

    public class CheckOptions
    {
        public List<CheckConfiguration> Checks { get; } = new();
    }

    public class CheckContext
    {
        private readonly CheckConfiguration m_Configuration;
        private readonly List<Finding> m_Findings;

        public CheckContext(
            SyntaxTree tree,
            ProjectOptions project,
            CheckConfiguration configuration,
            List<Finding> findings)
        {
            Tree = tree;
            Root = tree.GetRoot();
            Text = tree.GetText();
            Project = project;
            m_Configuration = configuration;
            m_Findings = findings;
        }

        public SyntaxTree Tree { get; }

        public SyntaxNode Root { get; }

        public SourceText Text { get; }

        public ProjectOptions Project { get; }

        public CheckSettings Settings
            => m_Configuration.Settings;

        // Уровень и метка включенной проверки
        public Severity Severity
            => m_Configuration.Severity;

        public string? Rule
            => m_Configuration.Rule;

        public void Report(TextSpan span, string message, LineReplacement? replacement = null)
            => Report(span, message, m_Configuration.Severity, m_Configuration.Rule, replacement);

        // Находка с уровнем и меткой правила конфига (запись правила проверки), а не проверки. Строки находки с
        // заменой - строки span и замены вместе
        public void Report(
            TextSpan span,
            string message,
            Severity severity,
            string? rule,
            LineReplacement? replacement = null)
        {
            var start = Text.Lines.GetLinePosition(span.Start);
            var end = Text.Lines.GetLinePosition(span.IsEmpty ? span.Start : span.End - 1);
            var first = replacement == null ? start.Line : Math.Min(start.Line, replacement.Start);
            var last = replacement == null ? end.Line : Math.Max(end.Line, replacement.End);
            m_Findings.Add(new Finding
            {
                Check = m_Configuration.Check.Name,
                Rule = rule,
                Severity = severity,
                Message = message,
                File = Tree.FilePath,
                Line = first + 1,
                EndLine = last + 1,
                Column = first == start.Line ? start.Character + 1 : 1,
                Replacement = replacement == null ? null : Widen(replacement, first, last),
            });
        }

        // Строки замены, дополненные исходными строками до first..last
        private List<string> Widen(LineReplacement replacement, int first, int last)
        {
            var lines = new List<string>();
            for (var line = first; line < replacement.Start; line++)
                lines.Add(Text.Lines[line].ToString());

            lines.AddRange(replacement.Lines);
            for (var line = replacement.End + 1; line <= last; line++)
                lines.Add(Text.Lines[line].ToString());

            return lines;
        }

        // Замена для находки: строки, которые задевает span, после подстановки newText вместо span
        public LineReplacement ReplaceInLines(TextSpan span, string newText)
            => ReplaceInLines(new[] { new TextChange(span, newText) });

        public LineReplacement ReplaceInLines(IReadOnlyList<TextChange> changes)
        {
            var first = Text.Lines.GetLineFromPosition(changes.Min(change => change.Span.Start));
            var last = Text.Lines.GetLineFromPosition(changes.Max(change => change.Span.End));
            var lines = Text.GetSubText(TextSpan.FromBounds(first.Start, last.End));
            var shifted = changes
                .Select(change => new TextChange(
                    new TextSpan(change.Span.Start - first.Start, change.Span.Length),
                    change.NewText ?? ""))
                .ToList();

            var changed = lines.WithChanges(shifted).ToString();
            var replaced = changed.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            return new LineReplacement(first.LineNumber, last.LineNumber, replaced);
        }

        // Замена строк start..end (индексы Text.Lines) целиком: проверка собирает новые строки сама
        public LineReplacement ReplaceLines(int start, int end, IReadOnlyList<string> lines)
            => new(start, end, lines);

        // Замена строк, которые задевает span, целиком
        public LineReplacement ReplaceLines(TextSpan span, IReadOnlyList<string> lines)
        {
            var start = Text.Lines.GetLineFromPosition(span.Start).LineNumber;
            var end = Text.Lines.GetLineFromPosition(span.IsEmpty ? span.Start : span.End - 1).LineNumber;
            return new LineReplacement(start, end, lines);
        }

        // Строки начала и конца span целиком - для находки с удалением строк
        public TextSpan GetLinesSpan(TextSpan span)
        {
            var first = Text.Lines.GetLineFromPosition(span.Start);
            var last = Text.Lines.GetLineFromPosition(span.IsEmpty ? span.Start : span.End - 1);
            return TextSpan.FromBounds(first.Start, last.End);
        }
    }
}
