using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Config
{
    // Общие сведения о проекте, на которые ссылаются режимы metrics, style и arch
    public class ProjectOptions
    {
        private IReadOnlyList<string> m_LoggerMethods = Array.Empty<string>();
        private Regex? m_LoggerPattern;

        public IReadOnlyList<string> PreprocessorSymbols { get; init; } = ["UNITY_EDITOR"];

        // Методы логгера: текст вызова без пробелов (Debug.Log, Log.Error); * - любые символы (*.Logger.Error)
        public IReadOnlyList<string> LoggerMethods
        {
            get => m_LoggerMethods;
            init
            {
                m_LoggerMethods = value;
                m_LoggerPattern = BuildPattern(value);
            }
        }

        // Механизмы проекта для признака повтора механизма; логгер с вызовами в обход - один из них
        public IReadOnlyList<ProjectMechanism> Mechanisms { get; init; } = Array.Empty<ProjectMechanism>();

        public CSharpParseOptions ParseOptions
            => new(LanguageVersion.Latest, DocumentationMode.Parse, SourceCodeKind.Regular, PreprocessorSymbols);

        public bool IsLoggerMethod(string invocationTarget)
            => m_LoggerPattern != null && m_LoggerPattern.IsMatch(invocationTarget);

        private static Regex? BuildPattern(IReadOnlyList<string> methods)
        {
            if (methods.Count == 0)
                return null;

            var alternatives = methods.Select(method => Regex.Escape(method).Replace("\\*", ".*"));
            return new Regex("^(?:" + string.Join("|", alternatives) + ")$", RegexOptions.CultureInvariant);
        }
    }
}
