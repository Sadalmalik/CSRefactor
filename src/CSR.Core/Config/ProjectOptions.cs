using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR.Core.Config
{
    // Общие сведения о проекте, на которые ссылаются режимы metrics, style и arch
    public class ProjectOptions
    {
        public IReadOnlyList<string> PreprocessorSymbols { get; init; } = ["UNITY_EDITOR"];

        public CSharpParseOptions ParseOptions
            => new(LanguageVersion.Latest, DocumentationMode.Parse, SourceCodeKind.Regular, PreprocessorSymbols);
    }
}
