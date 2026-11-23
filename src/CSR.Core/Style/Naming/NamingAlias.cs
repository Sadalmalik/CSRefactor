using CSR.Core.Checks;

namespace CSR.Core.Style.Naming
{
    // Старая проверка именования (до v0.4): прежние имя и параметры, правила naming - перевод параметров.
    // Уровень, метка и слои конфига - как у любой проверки; находки - с прежним именем проверки
    public class NamingAlias : SyntaxCheck
    {
        private readonly Func<CheckSettings, IReadOnlyList<NamingRule>> m_Translate;

        public NamingAlias(
            string name,
            string description,
            IReadOnlyList<CheckParameter> parameters,
            Func<CheckSettings, IReadOnlyList<NamingRule>> translate)
        {
            Name = name;
            Description = description;
            Parameters = parameters;
            m_Translate = translate;
        }

        public override string Name { get; }

        public override string Description { get; }

        public override IReadOnlyList<CheckParameter> Parameters { get; }

        public override string Deprecated
            => "naming (правила - DOCS.md, раздел naming, таблица перевода старых проверок)";

        public override void Analyze(CheckContext context)
            => NamingCheck.Run(context, m_Translate(context.Settings));
    }
}
