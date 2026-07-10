using CommandLine;

namespace PeopleMigrator;

/// <summary>
/// Builds a local People seed JSON from guest handle data (cache JSON, backup folder, or read-only Cosmos).
/// NEVER writes episode documents. Cosmos People writes require --persist-cosmos and --apply together.
/// </summary>
public class PeopleMigrationRequest
{
    [Option("cache-path", Required = false,
        HelpText = "Path to guest-handle-restore-cache.json (from EpisodeGuestHandleRestorer).")]
    public string? CachePath { get; set; }

    [Option("backup-path", Required = false,
        HelpText = "Episode backup folder for title/description name extraction. Defaults to cache backupPath when using --cache-path.")]
    public string? BackupPath { get; set; }

    [Option("from-cosmos", Required = false, Default = false,
        HelpText = "Read handle fields from Cosmos episodes (read-only). Use cache-path or backup-path when possible.")]
    public bool FromCosmos { get; set; }

    [Option("output", Required = false,
        HelpText = "Output path for people-seed.json (default: people-seed.json next to cache or backup folder).")]
    public string? OutputPath { get; set; }

    [Option("name-lookup", Required = false, Default = false,
        HelpText = "Scrape X profile pages and call Bluesky API to resolve display names.")]
    public bool NameLookup { get; set; }

    [Option("persist-cosmos", Required = false, Default = false,
        HelpText = "Write Person documents to Cosmos (requires --apply as well; default is JSON file only).")]
    public bool PersistCosmos { get; set; }

    [Option("apply", Required = false, Default = false,
        HelpText = "Second confirmation for Cosmos writes (requires --persist-cosmos; default is JSON file only).")]
    public bool Apply { get; set; }

    [Option("clear-people", Required = false, Default = false,
        HelpText = "Delete all documents in the People container first (requires --persist-cosmos and --apply).")]
    public bool ClearPeople { get; set; }

    [Option("sample", Required = false, Default = 15,
        HelpText = "Number of person records to show in console preview (0 = all).")]
    public int Sample { get; set; }

    [Option("clean-seed-from", Required = false,
        HelpText = "Post-process seed JSON: promote full names over titles/first-name-only canonicals and strip duplicate/noisy aliases.")]
    public string? CleanSeedFrom { get; set; }

    [Option("review-server", Required = false, Default = false,
        HelpText = "Start local web UI to review/edit a people-seed JSON file (no Cosmos writes).")]
    public bool ReviewServer { get; set; }

    [Option("seed-path", Required = false,
        HelpText = "Path to people-seed JSON for --review-server (default: people-seed.json in cwd).")]
    public string? SeedPath { get; set; }

    [Option("port", Required = false, Default = 5188,
        HelpText = "HTTP port for --review-server.")]
    public int Port { get; set; }

    [Option("enrich-aliases-from", Required = false,
        HelpText = "Alias-only pass: read an existing people-seed JSON, scan episode title/description, add aliases, write new seed.")]
    public string? EnrichAliasesFrom { get; set; }

    [Option("merge-seed-from", Required = false,
        HelpText = "Merge known duplicate rows in an existing people-seed JSON and write a new seed file.")]
    public string? MergeSeedFrom { get; set; }

    public bool WritesCosmos => PersistCosmos && Apply;

    public bool IsCleanSeedOnly => !string.IsNullOrWhiteSpace(CleanSeedFrom) && !ReviewServer && string.IsNullOrWhiteSpace(MergeSeedFrom);

    public bool IsMergeSeedOnly => !string.IsNullOrWhiteSpace(MergeSeedFrom) && !ReviewServer;

    public bool IsReviewServer => ReviewServer;

    public bool IsAliasEnrichmentOnly => !string.IsNullOrWhiteSpace(EnrichAliasesFrom) && !ReviewServer && string.IsNullOrWhiteSpace(MergeSeedFrom);
}
