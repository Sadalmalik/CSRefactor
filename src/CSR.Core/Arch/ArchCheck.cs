using CSR.Core.Checks;

namespace CSR.Core.Arch
{
    // Архитектурный признак по синтаксису одного файла. Находка - не ошибка, а повод решить:
    // сообщение говорит, что сработало, Question - что решить агенту или человеку.
    // Движок проверок - общий со style; у каждого признака есть параметры paths и excludePaths (их применяет движок)
    public abstract class ArchCheck : SyntaxCheck
    {
        private static readonly CheckParameter[] ScopeParameters = [FileScope.Paths, FileScope.ExcludePaths];

        private IReadOnlyList<CheckParameter>? m_Parameters;

        public abstract string Question { get; }

        public override IReadOnlyList<CheckParameter> Parameters
            => m_Parameters ??= ScopeParameters.Concat(OwnParameters).ToList();

        protected virtual IReadOnlyList<CheckParameter> OwnParameters
            => Array.Empty<CheckParameter>();
    }
}
