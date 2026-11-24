using System.Text.Json;
using CSR.Core.Structure;

namespace CSR.Reports.Config
{
    // structure.jsonc:
    // { "ownershipDepth": 3, "inheritanceDepth": 4, "godWmc": 47, "godAtfd": 5, "godTcc": 0.33,
    //   "dumpSize": 30, "dumpCommunities": 8, "infrastructureHosts": 5, "registryVariants": 30, "top": 10 }
    public static class StructureConfig
    {
        public static StructureOptions Parse(string json)
        {
            using var document = JsonConfig.Parse(json);
            var root = document.RootElement;
            JsonConfig.RequireObject(root, "structure");

            var options = new StructureOptions();
            foreach (var property in root.EnumerateObject())
            {
                var value = property.Value;
                switch (property.Name)
                {
                    case "ownershipDepth":
                        options.OwnershipDepth = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "inheritanceDepth":
                        options.InheritanceDepth = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "godWmc":
                        options.GodWmc = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "godAtfd":
                        options.GodAtfd = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "godTcc":
                        options.GodTcc = ReadShare(value, property.Name);
                        break;
                    case "dumpSize":
                        options.DumpSize = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "dumpCommunities":
                        options.DumpCommunities = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "infrastructureHosts":
                        options.InfrastructureHosts = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "registryVariants":
                        options.RegistryVariants = JsonConfig.ReadInt(value, property.Name);
                        break;
                    case "top":
                        options.Top = JsonConfig.ReadInt(value, property.Name);
                        break;
                    default:
                        throw new ConfigException($"неизвестный ключ '{property.Name}'");
                }
            }

            return options;
        }

        private static double ReadShare(JsonElement value, string path)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double share) || share is < 0 or > 1)
                throw new ConfigException($"{path}: ожидается число от 0 до 1");

            return share;
        }
    }
}
