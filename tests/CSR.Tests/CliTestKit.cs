using System.Diagnostics;
using System.Text;

namespace CSR.Tests
{
    public class CliResult
    {
        public required int ExitCode { get; init; }

        public required string Output { get; init; }

        public required string Error { get; init; }
    }

    // Временная папка теста; удаляется в Dispose
    public class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csr-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string Write(string relative, string text)
        {
            var file = System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, relative));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            File.WriteAllText(file, text);
            return file;
        }

        public string Read(string relative)
            => File.ReadAllText(System.IO.Path.Combine(Path, relative));

        public void Dispose()
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);

                Directory.Delete(Path, true);
            }
            catch (IOException)
            {
            }
        }
    }

    public static class Cli
    {
        // Program.Main с перехватом stdout и stderr; тесты CLI идут последовательно (консоль общая)
        public static CliResult Run(params string[] args)
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;
            var output = new StringWriter();
            var error = new StringWriter();
            Console.SetOut(output);
            Console.SetError(error);
            try
            {
                var code = Program.Main(args);
                return new CliResult { ExitCode = code, Output = output.ToString(), Error = error.ToString() };
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }

        // git в папке; без git в системе тест пропускается
        public static string Git(string folder, params string[] args)
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

            foreach (var arg in args)
                info.ArgumentList.Add(arg);

            Process? process;
            try
            {
                process = Process.Start(info);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Assert.Ignore("git не найден");
                return "";
            }

            using (process)
            {
                var text = process!.StandardOutput.ReadToEnd();
                var errorText = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Assert.That(process.ExitCode, Is.EqualTo(0), $"git {string.Join(" ", args)}: {errorText}");
                return text;
            }
        }

        // Репозиторий с одним коммитом: локальные имя и почта - тестовые
        public static void InitRepository(string folder)
        {
            Git(folder, "init", "-q");
            Git(folder, "config", "user.name", "CSR Tests");
            Git(folder, "config", "user.email", "tests@example.invalid");
            Git(folder, "config", "core.autocrlf", "false");
            Git(folder, "add", "-A");
            Git(folder, "commit", "-q", "-m", "initial");
        }
    }
}
