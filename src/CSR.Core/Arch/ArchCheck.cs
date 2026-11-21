using CSR.Core.Checks;

namespace CSR.Core.Arch
{
    // Архитектурный признак по синтаксису одного файла. Находка - не ошибка, а повод решить:
    // сообщение говорит, что сработало, Question - что решить агенту или человеку.
    // Движок проверок - общий со style; у каждого признака есть параметры paths и excludePaths
    public abstract class ArchCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] ScopeParameters =
        [
            CheckParameter.Strings("paths", "маски путей файлов, где признак ищется (* - любые символы); пусто - все"),
            CheckParameter.Strings("excludePaths", "маски путей файлов, где признак не ищется"),
        ];

        private IReadOnlyList<CheckParameter>? m_Parameters;

        public abstract string Question { get; }

        public override IReadOnlyList<CheckParameter> Parameters
            => m_Parameters ??= ScopeParameters.Concat(OwnParameters).ToList();

        protected virtual IReadOnlyList<CheckParameter> OwnParameters
            => Array.Empty<CheckParameter>();

        public override void Analyze(CheckContext context)
        {
            if (IsInScope(context.Settings, context.Tree.FilePath))
                AnalyzeFile(context);
        }

        // Файл попадает под paths и не попадает под excludePaths
        public static bool IsInScope(CheckSettings settings, string filePath)
        {
            var path = filePath.Replace('\\', '/');
            var included = NameMask.Get(settings.GetStrings("paths"), true);
            if (!included.IsEmpty && !included.IsMatch(path))
                return false;

            return !NameMask.Get(settings.GetStrings("excludePaths"), true).IsMatch(path);
        }

        protected abstract void AnalyzeFile(CheckContext context);
    }
}
