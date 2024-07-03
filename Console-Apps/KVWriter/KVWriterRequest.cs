using CommandLine;

namespace KVWriter;

public class KVWriterRequest
{
    [Option('i', "items", HelpText = "Number of items to take")]
    public int? ItemsToTake { get; set; }

    [Option('e', "episode-guid", HelpText = "Guid of Episode to create shortner-record for")]
    public Guid? EpisodeId { get; set; }
}