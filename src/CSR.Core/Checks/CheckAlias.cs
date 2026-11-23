namespace CSR.Core.Checks
{
    // Старая проверка (до удаления): прежние имя и параметры, анализ - перевод параметров в новую проверку.
    // Уровень, метка и слои конфига - как у любой проверки; находки - с прежним именем проверки
    public class CheckAlias : SyntaxCheck
    {
        private readonly string m_Replacement;
        private readonly Func<CheckSettings, Action<CheckContext>> m_Translate;
        private Translation? m_Last;

        // translate - перевод параметров в анализ одного файла
        public CheckAlias(
            string name,
            string description,
            string replacement,
            IReadOnlyList<CheckParameter> parameters,
            Func<CheckSettings, Action<CheckContext>> translate)
        {
            Name = name;
            Description = description;
            Parameters = parameters;
            m_Replacement = replacement;
            m_Translate = translate;
        }

        public override string Name { get; }

        public override string Description { get; }

        public override IReadOnlyList<CheckParameter> Parameters { get; }

        public override string Deprecated
            => m_Replacement;

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

            last.Analyze(context);
        }

        private class Translation
        {
            public Translation(CheckSettings settings, Action<CheckContext> analyze)
            {
                Settings = settings;
                Analyze = analyze;
            }

            public CheckSettings Settings { get; }

            public Action<CheckContext> Analyze { get; }
        }
    }
}
