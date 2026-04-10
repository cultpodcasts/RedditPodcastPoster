using CommandLine;

namespace FindDuplicateEpisodes;

public class FindDuplicateEpisodesRequest
{
    [Option('n', "not-dry-run", Required = false, Default = false,
        HelpText = "Execute all changes (delete/update duplicate episodes). Default is dry-run — no changes are made.")]
    public bool NotDryRun { get; set; }

    [Option("delete-no-diff", Required = false, Default = false,
        HelpText = "Delete pure duplicate episodes that have no meaningful field differences. Backs up each deleted episode to 'dedupe-episodes/{id}.json' before deletion. Pairs with differences are still reported but not changed.")]
    public bool DeleteNoDiff { get; set; }

    [Option('v', "verify-deduplication", Required = false, Default = false,
        HelpText = "Verify deduplication outcomes from backup files. Groups backups by podcastName and title, then checks canonical ignored/removed flags in Cosmos.")]
    public bool VerifyDeduplication { get; set; }
}
