using CommandLine;

namespace EnrichSinglePodcastFromPodcastServices;

public class EnrichPodcastRequest
{
    [Value(0, HelpText = "The id of the podcast", Required = true)]
    public Guid PodcastId { get; set; }

    [Option('r', "released-since", HelpText = "Items released within a certain number of days", Required = false)]
    public int? ReleasedSince { get; set; }
}