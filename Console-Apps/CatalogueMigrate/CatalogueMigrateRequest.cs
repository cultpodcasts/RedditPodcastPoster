using CommandLine;

namespace CatalogueMigrate;

public class CatalogueMigrateRequest
{
    public const int DefaultBatchSize = 25;
    public const int MaxBatchSize = 500;
    public const string DefaultEvidenceDirectory = "catalogue-migrate-evidence";

    [Option("kind", Required = false,
        HelpText = "NewsReport, Film, or TvShowEpisode. Run News then Film then TV (S-007). Not used with --rollback.")]
    public string Kind { get; set; } = string.Empty;

    [Option("podcast-id", Required = false,
        HelpText = "Move one podcast. Required for Film. Optional for News and TV (default is identify-scan).")]
    public Guid? PodcastId { get; set; }

    [Option("apply", Required = false, Default = false,
        HelpText = "Write Cosmos and swap Search. Refused (exit 2) unless --consent is the exact phrase in docs/catalogue-run/gate-5.md. Default is dry-run. " +
                   "Auto-indexed podcasts are moved only at minutes :15-:54 of the hour (the hourly indexer starts at :03 and :08); outside that they are skipped, so re-run in the window.")]
    public bool Apply { get; set; }

    [Option("consent", Required = false,
        HelpText = "Exact human consent phrase for --apply (see docs/catalogue-run/gate-5.md). Agents must never supply this.")]
    public string? Consent { get; set; }

    [Option("batch-size", Required = false, Default = DefaultBatchSize,
        HelpText = "Podcasts per evidence batch file (1-500).")]
    public int BatchSize { get; set; } = DefaultBatchSize;

    [Option("limit", Required = false,
        HelpText = "Stop after this many apply-eligible podcasts. Re-run to resume.")]
    public int? Limit { get; set; }

    [Option("evidence-dir", Required = false, Default = DefaultEvidenceDirectory,
        HelpText = "Folder for per-batch evidence (JSON lines). Written on --apply and --rollback --apply.")]
    public string EvidenceDirectory { get; set; } = DefaultEvidenceDirectory;

    [Option("rollback", Required = false,
        HelpText = "Path to an evidence batch file. Restores the podcasts it moved. Dry-run unless --apply plus the rollback consent phrase.")]
    public string? Rollback { get; set; }
}
