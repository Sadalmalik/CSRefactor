using System.Text;

namespace CSR
{
    public static class Output
    {
        // Отчет - в файл (UTF-8 без BOM) или в stdout
        public static void Write(string text, string? path)
        {
            if (path == null)
            {
                Console.Out.Write(text);
                return;
            }

            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
