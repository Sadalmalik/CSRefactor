namespace CSR.Core.Style
{
    // Текст находки запрета с альтернативой
    public static class ForbiddenNameTool
    {
        // "что: чем заменить"; пустая альтернатива - только что
        public static string WithUse(string message, string use)
            => use.Length > 0 ? $"{message}: {use}" : message;
    }
}
