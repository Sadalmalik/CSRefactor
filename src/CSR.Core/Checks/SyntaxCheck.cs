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

        public void Report(TextSpan span, string message, IReadOnlyList<string>? replacement = null)
        {
            var start = Text.Lines.GetLinePosition(span.Start);
            var end = Text.Lines.GetLinePosition(span.IsEmpty ? span.Start : span.End - 1);
            m_Findings.Add(new Finding
            {
                Check = m_Configuration.Check.Name,
                Rule = m_Configuration.Rule,
                Severity = m_Configuration.Severity,
                Message = message,
                File = Tree.FilePath,
                Line = start.Line + 1,
                EndLine = end.Line + 1,
                Column = start.Character + 1,
                Replacement = replacement,
            });
        }

        // Замена для находки: строки, которые задевает span, после подстановки newText вместо span
        public IReadOnlyList<string> ReplaceInLines(TextSpan span, string newText)
            => ReplaceInLines(new[] { new TextChange(span, newText) });

        public IReadOnlyList<string> ReplaceInLines(IReadOnlyList<TextChange> changes)
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
            return changed.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
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
