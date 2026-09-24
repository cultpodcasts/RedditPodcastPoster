namespace EpisodeWireRewrite;

/// <summary>
/// Repair evidence is mandatory on every migrate — never opt-in.
/// </summary>
public static class WireCutoverEvidencePaths
{
    public const string DefaultDirectoryName = "wire-cutover-evidence";

    /// <summary>
    /// Resolve the JSONL path for a migrate run. Always returns a concrete path.
    /// </summary>
    public static string ResolveMigrateJournalPath(string? journalOverride, DateTime utcNow)
    {
        if (!string.IsNullOrWhiteSpace(journalOverride))
        {
            return Path.GetFullPath(journalOverride);
        }

        var dir = Path.GetFullPath(DefaultDirectoryName);
        Directory.CreateDirectory(dir);
        var name = $"wire-cutover-{utcNow:yyyyMMdd-HHmmss}Z.jsonl";
        return Path.Combine(dir, name);
    }

    /// <summary>
    /// Rollback must point at an existing evidence pack (not auto-created).
    /// </summary>
    public static bool TryResolveRollbackJournalPath(string? journalOverride, out string path, out string error)
    {
        path = "";
        error = "";
        if (string.IsNullOrWhiteSpace(journalOverride))
        {
            error =
                "Rollback needs the evidence pack from the migrate run: pass --journal <path-to.jsonl> " +
                "(printed at the end of migrate). Repair evidence is always written on migrate; " +
                "rollback only selects which pack to reverse.";
            return false;
        }

        path = Path.GetFullPath(journalOverride);
        if (!File.Exists(path))
        {
            error = $"Evidence journal not found: {path}";
            return false;
        }

        return true;
    }
}
