using CSR.Core.Checks;

namespace CSR.Core.Style.Naming
{
    // Старая проверка именования (до v0.4): прежние имя и параметры, правила naming - перевод параметров.
    // Уровень, метка и слои конфига - как у любой проверки; находки - с прежним именем проверки
    public class NamingAlias : SyntaxCheck
    {
        private readonly Func<CheckSettings, IReadOnlyList<NamingRule>> m_Translate;
        private Translation? m_Last;

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

        // Перевод - один раз на набор параметров (параметры не меняются после загрузки конфига): разбор записей на
        // каждом файле заметен на прогоне в тысячи файлов. Без блокировки: гонка потоков только повторит перевод
        public override void Analyze(CheckContext context)
        {
            var last = m_Last;
            if (last == null || last.Settings != context.Settings)
            {
                last = new Translation(context.Settings, m_Translate(context.Settings));
                m_Last = last;
            }

            NamingCheck.Run(context, last.Rules);
        }

        private class Translation
        {
            public Translation(CheckSettings settings, IReadOnlyList<NamingRule> rules)
            {
                Settings = settings;
                Rules = rules;
            }

            public CheckSettings Settings { get; }

            public IReadOnlyList<NamingRule> Rules { get; }
        }
    }
}
