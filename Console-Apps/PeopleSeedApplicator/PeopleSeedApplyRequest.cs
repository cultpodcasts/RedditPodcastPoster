using CommandLine;

namespace PeopleSeedApplicator;

/// <summary>
/// Upserts Person documents from a reviewed people-seed JSON into the Cosmos People container only.
/// Default is dry-run; pass --apply to write.
/// </summary>
public class PeopleSeedApplyRequest
{
    [Option("seed-path", Required = true,
        HelpText = "Path to reviewed people-seed JSON (e.g. people-seed.iteration-9.json).")]
    public string SeedPath { get; set; } = string.Empty;

    [Option("apply", Required = false, Default = false,
        HelpText = "Write to Cosmos People container. Without this flag, dry-run only.")]
    public bool Apply { get; set; }
}
