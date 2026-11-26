using CSR.Core.Structure;

namespace CSR.Reports.Config
{
    // structure.jsonc:
    // { "ownershipDepth": 3, "inheritanceDepth": 4, "godWmc": 47, "godAtfd": 5, "godTcc": 0.33,
    //   "dumpSize": 30, "dumpCommunities": 8, "infrastructureHosts": 5, "registryVariants": 30,
    //   "minAssemblyLines": 0, "layerDepth": 0, "layers": { "Core": ["Core.*"] }, "top": 10 }
    public static class StructureConfig
    {
        public static readonly OptionTable<StructureOptions> Table = new OptionTable<StructureOptions>()
            .Add("ownershipDepth", OptionKind.Count, (options, value) => options.OwnershipDepth = value)
            .Add("inheritanceDepth", OptionKind.Count, (options, value) => options.InheritanceDepth = value)
            .Add("godWmc", OptionKind.Count, (options, value) => options.GodWmc = value)
            .Add("godAtfd", OptionKind.Count, (options, value) => options.GodAtfd = value)
            .Add("godTcc", OptionKind.Share, (options, value) => options.GodTcc = value)
            .Add("dumpSize", OptionKind.Count, (options, value) => options.DumpSize = value)
            .Add("dumpCommunities", OptionKind.Count, (options, value) => options.DumpCommunities = value)
            .Add("infrastructureHosts", OptionKind.Count, (options, value) => options.InfrastructureHosts = value)
            .Add("registryVariants", OptionKind.Count, (options, value) => options.RegistryVariants = value)
            .Add("minAssemblyLines", OptionKind.Count, (options, value) => options.MinAssemblyLines = value)
            .Add("layerDepth", OptionKind.Count, (options, value) => options.LayerDepth = value)
            .Add("layers", OptionKind.StringLists, (options, value) => options.Layers = value)
            .Add("top", OptionKind.Count, (options, value) => options.Top = value, "--top");

        public static StructureOptions Parse(string json)
        {
            var options = new StructureOptions();
            Table.ReadRoot(json, "structure", options);
            return options;
        }
    }
}
