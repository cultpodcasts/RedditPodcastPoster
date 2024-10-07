using CommandLine;

namespace RenamePodcast;

public class RenamePodcastRequest
{
    [Value(0, MetaName = "old-podcast-name", Required = true)]
    public required string OldPodcastName { get; set; }

    [Value(1, MetaName = "new-podcast-name", Required = true)]
    public required string NewPodcastName { get; set; }
}