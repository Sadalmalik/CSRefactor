using CSR.Core.Checks;
using CSR.Core.Style;

namespace CSR.Reports.Config
{
    // style.jsonc - конфиг проверок по каталогу StyleCatalog
    public static class StyleConfig
    {
        public static void Apply(string json, CheckOptions options)
            => CheckConfig.Apply(json, options, StyleCatalog.All);

        public static CheckOptions Parse(string json)
        {
            var options = new CheckOptions();
            Apply(json, options);
            return options;
        }
    }
}
