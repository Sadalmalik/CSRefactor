using CSR.Core.Arch;
using CSR.Core.Checks;
using CSR.Reports.Config;

namespace CSR.Reports.Arch
{
    // arch.jsonc - формат style.jsonc с признаками из ArchCatalog: { "checks": { "type-branch": { "rule": "A4" } } }.
    // Признак, которого нет в конфиге, выключен; конфиги накладываются по порядку
    public static class ArchConfig
    {
        public static void Apply(string json, CheckOptions options)
            => CheckConfig.Apply(json, options, ArchCatalog.All);

        public static CheckOptions Parse(string json)
        {
            var options = new CheckOptions();
            Apply(json, options);
            return options;
        }
    }
}
