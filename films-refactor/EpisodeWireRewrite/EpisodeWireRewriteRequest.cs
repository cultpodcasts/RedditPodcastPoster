using CommandLine;

namespace EpisodeWireRewrite;

public sealed class EpisodeWireRewriteRequest
{
    [Option("all", HelpText = "Scan the whole Episode container (stable ORDER BY id). Optional if --limit or --ids is set.")]
    public bool All { get; set; }

    [Option("apply", HelpText = "Persist patches. Default is dry-run. Repair evidence is always written.")]
    public bool Apply { get; set; }

    [Option("rollback", HelpText = "Restore cutover fields from a prior evidence journal's Before snapshots.")]
    public bool Rollback { get; set; }

    [Option("journal", HelpText =
        "Optional path override for the evidence JSONL. Migrate always writes a repair pack; " +
        "if omitted, a timestamped file under ./wire-cutover-evidence/ is created. " +
        "Rollback requires this (or the path of the pack from the migrate run).")]
    public string? Journal { get; set; }

    [Option("limit", Default = 0, HelpText =
        "Max episodes that still need cutover to work on (stable id order). " +
        "Already-migrated docs are skipped with a no-action log and do not count toward the limit. " +
        "0 = unlimited. Example: --limit 10 --apply for a small live batch.")]
    public int Limit { get; set; }

    [Option("progress-every", Default = 100, HelpText = "Print a progress line every N documents.")]
    public int ProgressEvery { get; set; } = 100;

    [Option("dop", Default = 8, HelpText = "Max degree of parallelism (rollback only; migrate is sequential for stable order).")]
    public int DegreeOfParallelism { get; set; } = 8;

    [Option("ids", HelpText = "Comma-separated episode ids (optional; otherwise --all / --limit).")]
    public string? Ids { get; set; }

    [Option("version", HelpText = "Print version and exit.")]
    public bool Version { get; set; }
}
