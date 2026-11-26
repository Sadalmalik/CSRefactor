using System.Diagnostics;
using CSR.Workspace.Loading;

namespace CSR.Workspace.Tests
{
    // Копия тестового решения TestData/Sample во временной папке: пакеты восстановлены, решение не собрано
    public class SampleSolution : IDisposable
    {
        private static readonly Lazy<Task<LoadedSolution>> SharedLoad = new(LoadFirstAsync);
        private static SampleSolution? s_Shared;

        private SampleSolution(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public string SolutionPath
            => Path.Combine(Root, "Sample.sln");

        // Одна загрузка на прогон для тестов, которые ничего не пишут
        public static Task<LoadedSolution> LoadSharedAsync()
            => SharedLoad.Value;

        public static void DisposeShared()
        {
            if (SharedLoad.IsValueCreated && SharedLoad.Value.IsCompletedSuccessfully)
                SharedLoad.Value.Result.Dispose();

            s_Shared?.Dispose();
        }

        public static SampleSolution Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "csr-tests", "sample-" + Guid.NewGuid().ToString("N"));
            Copy(Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData", "Sample"), root);
            var sample = new SampleSolution(root);
            sample.Restore();
            return sample;
        }

        public string GetPath(string relative)
            => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

        public Task<LoadedSolution> LoadAsync()
            => SolutionLoader.LoadAsync(SolutionPath);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static async Task<LoadedSolution> LoadFirstAsync()
        {
            s_Shared = Create();
            return await s_Shared.LoadAsync();
        }

        public void Restore()
        {
            var info = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            info.ArgumentList.Add("restore");
            info.ArgumentList.Add(SolutionPath);
            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"dotnet restore: {output.Result}\n{error.Result}");
        }

        private static void Copy(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.EnumerateFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));

            foreach (var folder in Directory.EnumerateDirectories(source))
                Copy(folder, Path.Combine(target, Path.GetFileName(folder)));
        }
    }

    [SetUpFixture]
    public class SampleCleanup
    {
        [OneTimeTearDown]
        public void TearDown()
            => SampleSolution.DisposeShared();
    }
}
