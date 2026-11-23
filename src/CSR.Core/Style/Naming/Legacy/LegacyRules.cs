using CSR.Core.Checks;

namespace CSR.Core.Style.Naming.Legacy
{
    // Правила naming для перевода старых проверок именования (до v0.4)
    public static class LegacyRules
    {
        // Правило с одним требованием для символов видов kinds
        public static NamingRule Create(string kinds, NameRequirementKind kind, INameRequirement requirement)
            => Create(Selector(new() { ["kinds"] = [kinds] }), kind, requirement);

        public static NamingRule Create(
            NameSelector symbols,
            NameRequirementKind kind,
            INameRequirement requirement,
            string? label = null)
        {
            var requirements = new Dictionary<string, INameRequirement> { [kind.Key] = requirement };
            return new NamingRule(symbols, requirements, label: label);
        }

        // Селектор из условий: kinds, access, modifiers, types, attributes, bases
        public static NameSelector Selector(Dictionary<string, IReadOnlyList<string>> fields)
        {
            var record = new Record(fields.ToDictionary(field => field.Key, field => (object)field.Value));
            return NameSelector.Parse(record);
        }
    }
}
