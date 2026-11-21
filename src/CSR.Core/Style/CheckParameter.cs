namespace CSR.Core.Style
{
    public enum ParameterKind
    {
        Int,
        Bool,
        String,
        Strings,
        Map,
    }

    // Параметр проверки: имя, вид значения, значение по умолчанию и допустимые значения строки (для prefer)
    public class CheckParameter
    {
        public required string Name { get; init; }

        public required ParameterKind Kind { get; init; }

        public required object Default { get; init; }

        public IReadOnlyList<string>? AllowedValues { get; init; }

        public required string Description { get; init; }

        public static CheckParameter Int(string name, int value, string description)
            => new() { Name = name, Kind = ParameterKind.Int, Default = value, Description = description };

        public static CheckParameter Bool(string name, bool value, string description)
            => new() { Name = name, Kind = ParameterKind.Bool, Default = value, Description = description };

        public static CheckParameter Choice(string name, string description, params string[] values)
        {
            return new CheckParameter
            {
                Name = name,
                Kind = ParameterKind.String,
                Default = values[0],
                AllowedValues = values,
                Description = description,
            };
        }

        public static CheckParameter Strings(string name, string description, params string[] values)
            => new() { Name = name, Kind = ParameterKind.Strings, Default = values, Description = description };

        public static CheckParameter Map(string name, IReadOnlyDictionary<string, string> value, string description)
            => new() { Name = name, Kind = ParameterKind.Map, Default = value, Description = description };
    }

    // Значения параметров одной проверки; незаданные - значения по умолчанию из CheckParameter
    public class CheckSettings
    {
        private readonly IReadOnlyList<CheckParameter> m_Parameters;
        private readonly Dictionary<string, object> m_Values = new();

        public CheckSettings(IReadOnlyList<CheckParameter> parameters)
        {
            m_Parameters = parameters;
        }

        public void Set(string name, object value)
            => m_Values[name] = value;

        public int GetInt(string name)
            => (int)Get(name);

        public bool GetBool(string name)
            => (bool)Get(name);

        public string GetString(string name)
            => (string)Get(name);

        public IReadOnlyList<string> GetStrings(string name)
            => (IReadOnlyList<string>)Get(name);

        public IReadOnlyDictionary<string, string> GetMap(string name)
            => (IReadOnlyDictionary<string, string>)Get(name);

        public CheckSettings Clone()
        {
            var clone = new CheckSettings(m_Parameters);
            foreach (var pair in m_Values)
                clone.m_Values[pair.Key] = pair.Value;
            return clone;
        }

        private object Get(string name)
        {
            if (m_Values.TryGetValue(name, out object? value))
                return value;

            var parameter = m_Parameters.FirstOrDefault(candidate => candidate.Name == name);
            if (parameter == null)
                throw new ArgumentException($"у проверки нет параметра '{name}'", nameof(name));

            return parameter.Default;
        }
    }
}
