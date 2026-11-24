using CSR.Core.Structure;

namespace CSR.Reports.Config
{
    // structure.jsonc:
    // { "ownershipDepth": 3, "inheritanceDepth": 4, "godWmc": 47, "godAtfd": 5, "godTcc": 0.33,
    //   "dumpSize": 30, "dumpCommunities": 8, "infrastructureHosts": 5, "registryVariants": 30, "top": 10 }
    public static class StructureConfig
    {
        public static readonly OptionTable<StructureOptions> Table = new OptionTable<StructureOptions>()
            .Add("ownershipDepth", OptionKinds.Count, (options, value) => options.OwnershipDepth = value)
            .Add("inheritanceDepth", OptionKinds.Count, (options, value) => options.InheritanceDepth = value)
            .Add("godWmc", OptionKinds.Count, (options, value) => options.GodWmc = value)
            .Add("godAtfd", OptionKinds.Count, (options, value) => options.GodAtfd = value)
            .Add("godTcc", OptionKinds.Share, (options, value) => options.GodTcc = value)
            .Add("dumpSize", OptionKinds.Count, (options, value) => options.DumpSize = value)
            .Add("dumpCommunities", OptionKinds.Count, (options, value) => options.DumpCommunities = value)
            .Add("infrastructureHosts", OptionKinds.Count, (options, value) => options.InfrastructureHosts = value)
            .Add("registryVariants", OptionKinds.Count, (options, value) => options.RegistryVariants = value)
            .Add("top", OptionKinds.Positive, (options, value) => options.Top = value, "--top");

        public static StructureOptions Parse(string json)
        {
            var options = new StructureOptions();
            Table.ReadRoot(json, "structure", options);
            return options;
        }
    }
}
