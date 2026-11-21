using CSR.Core.Checks;
using CSR.Core.Config;
using Microsoft.CodeAnalysis;

namespace CSR.Core.Arch
{
    // Архитектурный признак по всему решению: на вход - компиляции проектов (семантика Roslyn), без загрузки
    // решения в ядре. В режиме файлов (csr arch без --solution) не работает. Конфиг - тот же arch.jsonc
    public abstract class SolutionCheck : ArchCheck
    {
        public abstract void AnalyzeSolution(SolutionCheckContext context);

        protected override void AnalyzeFile(CheckContext context)
        {
        }
    }

    public class SolutionCheckContext
    {
        private readonly CheckConfiguration m_Configuration;
        private readonly List<Finding> m_Findings;
        private readonly Func<string, string> m_DisplayPath;

        public SolutionCheckContext(SolutionIndex index, IReadOnlyList<Compilation> compilations,
            ProjectOptions project, CheckConfiguration configuration, List<Finding> findings,
            Func<string, string> displayPath, CancellationToken cancellation)
        {
            Index = index;
            Compilations = compilations;
            Project = project;
            m_Configuration = configuration;
            m_Findings = findings;
            m_DisplayPath = displayPath;
            Cancellation = cancellation;
        }

        // Типы и ссылки выбранных проектов; общий для всех признаков прогона
        public SolutionIndex Index { get; }

        // Компиляции выбранных проектов: их файлы - область поиска и подсчета
        public IReadOnlyList<Compilation> Compilations { get; }

        public ProjectOptions Project { get; }

        public CancellationToken Cancellation { get; }

        public CheckSettings Settings
            => m_Configuration.Settings;

        // Путь файла для отчета (относительный, с '/')
        public string GetDisplayPath(string filePath)
            => m_DisplayPath(filePath);

        // Находка в исходнике; место вне исходников или вне paths / excludePaths признака не попадает в отчет
        public void Report(Location location, string message)
        {
            if (!location.IsInSource || location.SourceTree == null)
                return;

            var file = GetDisplayPath(location.SourceTree.FilePath);
            if (!ArchCheck.IsInScope(Settings, file))
                return;

            var span = location.GetLineSpan();
            m_Findings.Add(new Finding
            {
                Check = m_Configuration.Check.Name,
                Rule = m_Configuration.Rule,
                Severity = m_Configuration.Severity,
                Message = message,
                File = file,
                Line = span.StartLinePosition.Line + 1,
                EndLine = span.EndLinePosition.Line + 1,
                Column = span.StartLinePosition.Character + 1,
            });
        }
    }
}
