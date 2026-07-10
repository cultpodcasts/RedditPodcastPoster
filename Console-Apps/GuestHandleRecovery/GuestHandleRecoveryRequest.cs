using CommandLine;

namespace GuestHandleRecovery;

public class GuestHandleRecoveryRequest
{
    [Option('b', "backup-dir", Required = false,
        Default = @"C:\Users\jonbr\source\repos\CultPodcasts-PrivateDatabase\2026-06-12\episode",
        HelpText = "Directory of per-episode JSON backup files.")]
    public string BackupDir { get; set; } = string.Empty;

    [Option('d', "dry-run", Required = false, Default = false,
        HelpText = "Report episodes that would be patched without writing to Cosmos.")]
    public bool DryRun { get; set; }

    [Option("verify-id", Required = false,
        HelpText = "After run, read this episode id from Cosmos and log handle fields.")]
    public Guid? VerifyEpisodeId { get; set; }
}
