using CSR.Core.Arch;
using CSR.Core.Checks;

namespace CSR.Core.Style
{
    // Параметр excludePaths проверок стиля: маски путей файлов, где проверка не работает (редакторный код, тесты)
    public static class FileScope
    {
        public static readonly CheckParameter ExcludePaths = CheckParameter.Strings(
            "excludePaths",
            "маски путей файлов, где проверка не работает (* - любые символы, разделитель /)");

        // Путь от текущей папки (Editor/Tool.cs) проверяется и с ведущим /: маска */Editor/* ловит и папку верхнего уровня
        public static bool IsExcluded(CheckContext context)
        {
            var path = context.Tree.FilePath.Replace('\\', '/');
            var masks = NameMask.Get(context.Settings.GetStrings("excludePaths"), true);
            return masks.IsMatch(path) || masks.IsMatch("/" + path);
        }
    }
}
