using CommandLine;

namespace EpisodeServiceBackfill;

public class EpisodeServiceBackfillRequest
{
    [Option('a', "apply", Required = false, Default = false,
        HelpText = "Apply Cosmos saves. Without this flag the run is dry-run only.")]
    public bool Apply { get; set; }

    [Option("limit", Required = false, Default = 4,
        HelpText = "Maximum candidate documents to select (and apply when --apply).")]
    public int Limit { get; set; }

    [Option("ids", Required = false, Default = null,
        HelpText = "Comma-separated episode ids. When omitted, the first --limit NeedsBackfill documents from a Cosmos scan are used.")]
    public string? Ids { get; set; }

    [Option("scan", Required = false, Default = 80,
        HelpText = "How many Cosmos documents to scan when --ids and --all are omitted.")]
    public int Scan { get; set; }

    [Option("all", Required = false, Default = false,
        HelpText = "Scan every episode in the container. Quiet progress lines; does not print patches.")]
    public bool All { get; set; }

    [Option("show-patches", Required = false, Default = false,
        HelpText = "Print services/ids patch JSON. On by default when --ids is set.")]
    public bool ShowPatches { get; set; }

    [Option("progress-every", Required = false, Default = 1000,
        HelpText = "Write a progress line every N scanned documents during --all.")]
    public int ProgressEvery { get; set; }

    [Option("snapshot-dir", Required = false, Default = null,
        HelpText = "Directory to write before/after JSON slices. Created if missing.")]
    public string? SnapshotDir { get; set; }

    [Option("dop", Required = false, Default = 8,
        HelpText = "Max parallel patches (CPU + PatchItemAsync). Cosmos query iterator stays sequential. Minimum 1.")]
    public int DegreeOfParallelism { get; set; } = 8;

    [Option("spot-check", Required = false, Default = 1000,
        HelpText = "Reservoir-sample this many patched (or candidate) episodes and re-read them after the run. If fewer candidates, sample all.")]
    public int SpotCheck { get; set; } = 1000;

    [Option("patch-log", Required = false, Default = "episode-service-backfill-patches.jsonl",
        HelpText = "JSONL log of patch episode/podcast ids. Overwritten at the start of each run. With --classify-skips this is the skip-reason output path (created, not the candidate patch log).")]
    public string PatchLog { get; set; } = "episode-service-backfill-patches.jsonl";

    [Option("classify-skips", Required = false, Default = false,
        HelpText = "Read-only: diff Cosmos episode ids against --candidates-from and classify documents that were not candidates. Does not apply patches.")]
    public bool ClassifySkips { get; set; }

    [Option("candidates-from", Required = false, Default = null,
        HelpText = "Read-only JSONL of prior candidate episode ids (the apply patch log). Required with --classify-skips unless --before-ts is set.")]
    public string? CandidatesFrom { get; set; }

    [Option("before-ts", Required = false, Default = 0,
        HelpText = "With --classify-skips: SELECT * FROM c WHERE c._ts < this Unix-seconds value instead of a full id scan.")]
    public long BeforeTs { get; set; }

    [Option("after-ts", Required = false, Default = 0,
        HelpText = "With --classify-skips: also count documents with c._ts > this Unix-seconds value (post-run writes).")]
    public long AfterTs { get; set; }

    [Option("since-ts", Required = false, Default = 0,
        HelpText = "SELECT * FROM c WHERE c._ts > this Unix-seconds value and classify NeedsBackfill. With --apply, patch those candidates (surgical services/ids). Does not overwrite episode-service-backfill-patches.jsonl.")]
    public long SinceTs { get; set; }

    [Option("version", HelpText = "Display version information")]
    public bool Version { get; set; }
}
