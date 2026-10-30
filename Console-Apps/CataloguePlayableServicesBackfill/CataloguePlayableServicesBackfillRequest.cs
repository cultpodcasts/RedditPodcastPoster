using CommandLine;

namespace CataloguePlayableServicesBackfill;

public class CataloguePlayableServicesBackfillRequest
{
    [Option("episode-backup-dir", Required = false,
        HelpText = "Root that contains episode/{guid}.json (CosmosDbDownloader layout), or the episode folder itself. At least one of --episode-backup-dir or --public-database-dir is required.")]
    public string? EpisodeBackupDir { get; set; }

    [Option("public-database-dir", Required = false,
        HelpText = "PublicDatabase checkout (flat *.json). Indexes playable/episode ids and their urls map as a second truth source (urls only; no images). At least one of --episode-backup-dir or --public-database-dir is required.")]
    public string? PublicDatabaseDir { get; set; }

    [Option("youtube-id-only", Required = false, Default = false,
        HelpText = "Only sync the compact youTubeId from services.youtube.url (no truth sources needed; services are not changed). --episode-backup-dir / --public-database-dir are ignored.")]
    public bool YouTubeIdOnly { get; set; }

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
