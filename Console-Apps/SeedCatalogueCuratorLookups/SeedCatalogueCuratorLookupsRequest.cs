using CommandLine;

namespace SeedCatalogueCuratorLookups;

public class SeedCatalogueCuratorLookupsRequest
{
    [Option('a', "apply", Required = false, Default = false,
        HelpText = "Write the four catalogue curator LookUps documents. Without this flag the run is a dry-run report only.")]
    public bool Apply { get; set; }

    [Option('f', "force", Required = false, Default = false,
        HelpText = "Overwrite existing LookUps documents. Without --force, existing documents are left unchanged.")]
    public bool Force { get; set; }
}
