using CommandLine;

namespace KVWriter;

public class KVWriterRequest
{
    [Option('i', "items", HelpText = "Number of items to take", Group = "batch")]
    public int? ItemsToTake { get; set; }

    [Option('s', "skip", HelpText = "Number of items to skip", Default = 0, Group = "batch")]
    public int ItemsToSkip { get; set; }

    [Option('e', "episode-guid", HelpText = "Guid of Episode to create shortner-record for", Group = "single")]
    public Guid? EpisodeId { get; set; }

    [Option('d', "dry-run", Default = false, HelpText = "Dry-Run", Group = "single")]
    public bool IsDryRun { get; set; }

    [Option('r', "read", HelpText = "Short-guid for the item to read")]
    public string? Key { get; set; }
}