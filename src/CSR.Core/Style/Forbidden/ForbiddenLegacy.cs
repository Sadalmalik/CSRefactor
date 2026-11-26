using CSR.Core.Checks;

namespace CSR.Core.Style.Forbidden
{
    // forbidden-call и forbidden-type до v0.4: маски с текстом альтернативы -> записи forbidden
    public static class ForbiddenLegacy
    {
        private const string Replacement = "forbidden (записи - DOCS.md, раздел forbidden, таблица перевода старых проверок)";

        public static readonly CheckAlias Calls = Create(
            "forbidden-call",
            "запрещенный вызов или член (устарела: forbidden)",
            "calls",
            "маска Тип.Член (* - любые символы) и чем заменить",
            ForbiddenKind.Call);

        public static readonly CheckAlias Types = Create(
            "forbidden-type",
            "запрещенный тип (устарела: forbidden)",
            "types",
            "маска имени типа (* - любые символы) и чем заменить",
            ForbiddenKind.Type);

        private static CheckAlias Create(
            string name,
            string description,
            string map,
            string mapDescription,
            string kind)
        {
            return new CheckAlias(
                name,
                description,
                Replacement,
                [
                    CheckParameter.Map(map, new Dictionary<string, string>(), mapDescription),
                    FileScope.ExcludePaths,
                ],
                settings => Translate(settings, map, kind));
        }

        // Пара маски -> запись того же вида; excludePaths проверки применяет движок
        private static Action<CheckContext> Translate(CheckSettings settings, string map, string kind)
        {
            var entries = settings.GetMap(map)
                .Select(pair => ForbiddenEntry.Parse(new Record(new Dictionary<string, object>
                {
                    ["kind"] = kind,
                    ["mask"] = pair.Key,
                    ["use"] = pair.Value,
                })))
                .ToList();
            return context => ForbiddenCheck.Run(context, entries);
        }
    }
}
