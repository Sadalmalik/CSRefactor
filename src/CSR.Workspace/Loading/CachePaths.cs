using System.Security.Cryptography;
using System.Text;

namespace CSR.Workspace.Loading
{
    // Кэш CSR вне кода проекта: теневые копии решений, obj, реестр серверов.
    // Корень - переменная CSR_CACHE или %LOCALAPPDATA%\csr
    public static class CachePaths
    {
        public static string Root
        {
            get
            {
                var custom = Environment.GetEnvironmentVariable("CSR_CACHE");
                if (!string.IsNullOrEmpty(custom))
                    return custom;

                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(local, "csr");
            }
        }

        public static string Servers
            => Path.Combine(Root, "servers");

        public static string ForSolution(string solutionPath)
        {
            var name = Path.GetFileNameWithoutExtension(solutionPath);
            return Path.Combine(Root, "solutions", $"{name}-{GetKey(solutionPath)}");
        }

        // Ключ решения: хеш нормализованного полного пути
        public static string GetKey(string solutionPath)
        {
            var normalized = Path.GetFullPath(solutionPath).ToLowerInvariant();
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
        }
    }
}
