using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CSR
{
    // Компиляция из файлов без загрузки решения: деревья цели и сборки платформы .NET, на которой запущен csr.
    // Типы исходного кода разрешаются по пространствам и using; сторонние библиотеки (Unity, пакеты) не подключаются -
    // их типы неразрешены, связи с ними в граф типов не входят
    public static class SourceCompilation
    {
        private static readonly Lazy<IReadOnlyList<MetadataReference>> PlatformReferences = new(LoadPlatformReferences);

        public static CSharpCompilation Create(IEnumerable<SyntaxTree> trees)
        {
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);
            return CSharpCompilation.Create("csr-source", trees, PlatformReferences.Value, options);
        }

        private static IReadOnlyList<MetadataReference> LoadPlatformReferences()
        {
            var paths = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "";
            return paths.Split(Path.PathSeparator)
                .Where(path => Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal)
                    || Path.GetFileName(path) is "netstandard.dll" or "mscorlib.dll")
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToList();
        }
    }
}
