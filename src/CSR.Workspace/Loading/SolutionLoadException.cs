namespace CSR.Workspace.Loading
{
    // Решение не загрузилось из-за окружения (нет dotnet или MSBuild для BuildHost): ошибка запуска, не сбой csr
    public class SolutionLoadException : Exception
    {
        public SolutionLoadException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
