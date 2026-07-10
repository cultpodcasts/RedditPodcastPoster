using CommandLine;

namespace EpisodeGuestHandleRestorer;

public class EpisodeGuestHandleRestorerRequest
{
    [Option("backup-path", Required = false,
        Default = @"C:\Users\jonbr\source\repos\CultPodcasts-PrivateDatabase\2026-06-12\episode",
        HelpText = "Directory of per-episode JSON backup files.")]
    public string BackupPath { get; set; } = string.Empty;

    [Option("dry-run", Required = false, Default = true,
        HelpText = "Report episodes that would be patched without writing to Cosmos (default).")]
    public bool DryRun { get; set; } = true;

    [Option("apply", Required = false, Default = false,
        HelpText = "Apply surgical handle patches to production Cosmos. Requires explicit flag.")]
    public bool Apply { get; set; }

    [Option("cache-path", Required = false,
        HelpText = "Path for patch-plan cache JSON. Default: guest-handle-restore-cache.json next to backup-path.")]
    public string? CachePath { get; set; }

    [Option("use-cache", Required = false, Default = false,
        HelpText = "Apply mode only: load patch plan from cache instead of scanning ~90k backup files.")]
    public bool UseCache { get; set; }
}
