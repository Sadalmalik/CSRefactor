using CSR.Core.Arch;
using CSR.Core.Style;
using CSR.Reports.Config;

namespace CSR.Reports.Arch
{
    // arch.jsonc - формат style.jsonc с признаками из ArchCatalog: { "checks": { "type-branch": { "rule": "A4" } } }.
    // Признак, которого нет в конфиге, выключен; конфиги накладываются по порядку
    public static class ArchConfig
    {
        public static void Apply(string json, StyleOptions options)
            => StyleConfig.Apply(json, options, ArchCatalog.All);

        public static StyleOptions Parse(string json)
        {
            var options = new StyleOptions();
            Apply(json, options);
            return options;
        }
    }
}
