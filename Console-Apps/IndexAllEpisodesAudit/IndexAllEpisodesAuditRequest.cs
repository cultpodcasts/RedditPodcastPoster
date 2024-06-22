using CommandLine;

namespace IndexAllEpisodesAudit;

public class IndexAllEpisodesAuditRequest
{
    [Value(0,
        HelpText =
            "The minimum amount of time since the last episode was released to deem this podcast-series as requiring indexing of all episodes",
        Required = true)]
    public TimeSpan Since { get; set; }

    [Option('d', Required = false, Default = false, HelpText = "Execute dry run")]
    public bool DryRun { get; set; }
}