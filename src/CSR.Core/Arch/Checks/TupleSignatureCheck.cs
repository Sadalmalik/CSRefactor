using CSR.Core.Checks;
using CSR.Core.Style.Checks;

namespace CSR.Core.Arch.Checks
{
    // Кортеж в сигнатуре или поле - та же проверка, что tuple-type в style, с вопросом-решением
    public class TupleSignatureCheck : ArchCheck
    {
        private readonly TupleTypeCheck m_Check = new();

        public override string Name
            => m_Check.Name;

        public override string Description
            => m_Check.Description;

        public override string Question
            => "Несколько результатов - именованная структура или out?";

        public override void Analyze(CheckContext context)
            => m_Check.Analyze(context);
    }
}
