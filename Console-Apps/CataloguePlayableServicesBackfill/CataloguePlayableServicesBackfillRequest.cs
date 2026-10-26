using CommandLine;

namespace CataloguePlayableServicesBackfill;

public class CataloguePlayableServicesBackfillRequest
{
    [Option("episode-backup-dir", Required = true,
        HelpText = "Root that contains episode/{guid}.json (CosmosDbDownloader layout), or the episode folder itself.")]
    public string EpisodeBackupDir { get; set; } = string.Empty;

    [Option("kind", Required = false, Default = "all",
        HelpText = "film | tvshowepisode | newsreport | all.")]
    public string Kind { get; set; } = "all";

    [Option('a', "apply", Required = false, Default = false,
        HelpText = "Write Cosmos (+ search). Without this flag the run is dry-run only.")]
    public bool Apply { get; set; }

    [Option("consent", Required = false, Default = null,
        HelpText = "Exact human consent phrase for --apply. Agents must never supply this.")]
    public string? Consent { get; set; }

    [Option("change-log", Required = false, Default = "catalogue-playable-services-backfill.jsonl",
        HelpText = "JSONL change log path. Overwritten at the start of each run.")]
    public string ChangeLog { get; set; } = "catalogue-playable-services-backfill.jsonl";

    [Option("limit", Required = false, Default = 0,
        HelpText = "Stop after this many discrepancy candidates (0 = no limit).")]
    public int Limit { get; set; }

    [Option("ids", Required = false, Default = null,
        HelpText = "Comma-separated playable ids. When set, only those documents are considered.")]
    public string? Ids { get; set; }

    [Option("progress-every", Required = false, Default = 1000,
        HelpText = "Write a progress line every N scanned documents.")]
    public int ProgressEvery { get; set; } = 1000;

    [Option("snapshot-dir", Required = false, Default = null,
        HelpText = "Optional directory for before Services JSON snapshots of changed playables.")]
    public string? SnapshotDir { get; set; }
}
