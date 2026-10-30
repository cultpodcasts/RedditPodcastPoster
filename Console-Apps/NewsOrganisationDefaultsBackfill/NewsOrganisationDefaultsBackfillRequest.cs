using CommandLine;

namespace NewsOrganisationDefaultsBackfill;

public class NewsOrganisationDefaultsBackfillRequest
{
    [Option('a', "apply", Required = false, Default = false,
        HelpText = "Write the flags. Without this flag the run is a dry-run report only.")]
    public bool Apply { get; set; }
}
