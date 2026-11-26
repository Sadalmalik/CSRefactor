namespace CSR
{
    // Предопределенные сборки Unity для кода в Assets без asmdef (порядок компиляции особых папок): Plugins,
    // Standard Assets, Pro Standard Assets первого уровня - firstpass, папка Editor на любом уровне - Editor
    public static class UnityAssemblyTool
    {
        private static readonly string[] FirstPassFolders = ["Plugins", "Standard Assets", "Pro Standard Assets"];

        // Корень проекта Unity: в папке есть Assets и ProjectSettings
        public static bool IsProjectRoot(string folder)
            => Directory.Exists(Path.Combine(folder, "Assets")) && Directory.Exists(Path.Combine(folder, "ProjectSettings"));

        // Папка Assets проекта Unity
        public static bool IsAssetsRoot(string folder)
        {
            var parent = Path.GetDirectoryName(folder);
            return parent != null
                && string.Equals(Path.GetFileName(folder), "Assets", StringComparison.OrdinalIgnoreCase)
                && IsProjectRoot(parent);
        }

        // folder - папка файла внутри assets
        public static string GetName(string assets, string folder)
        {
            var folders = Path.GetRelativePath(assets, folder).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool isFirstPass = FirstPassFolders.Contains(folders[0], StringComparer.OrdinalIgnoreCase);
            bool isEditor = folders.Contains("Editor", StringComparer.OrdinalIgnoreCase);
            return (isFirstPass, isEditor) switch
            {
                (true, true) => "Assembly-CSharp-Editor-firstpass",
                (true, false) => "Assembly-CSharp-firstpass",
                (false, true) => "Assembly-CSharp-Editor",
                (false, false) => "Assembly-CSharp",
            };
        }
    }
}
