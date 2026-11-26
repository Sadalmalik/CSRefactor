namespace CSR.Core.Similar
{
    // Ключи видов единиц в конфиге, командной строке и отчете
    public static class UnitKindTool
    {
        private static readonly Dictionary<UnitKind, string> Keys = new()
        {
            [UnitKind.Member] = "member",
            [UnitKind.Fragment] = "fragment",
            [UnitKind.ParameterSet] = "parameters",
            [UnitKind.FieldSet] = "fields",
            [UnitKind.Type] = "type",
            [UnitKind.System] = "system",
        };

        public static IEnumerable<string> AllKeys
            => Keys.Values;

        public static string GetKey(UnitKind kind)
            => Keys[kind];

        // Вид по ключу; null - неизвестный ключ
        public static UnitKind? Find(string key)
        {
            foreach (var pair in Keys)
            {
                if (pair.Value == key)
                    return pair.Key;
            }

            return null;
        }
    }
}
