using CommandLine;

namespace EliminateExistingEpisodes;

public class Request
{
    [Option('p', "podcast-id", Required = false, HelpText = "The id of the podcast to index")]
    public Guid? PodcastId { get; set; }

    [Option('n', "podcast-name", Required = false, HelpText = "The name of the podcast to index")]
    public string? PodcastName { get; set; }

    [Option('r', "released-since", Default = 2, HelpText = "Will index episodes released within this many days")]
    public int ReleasedSince { get; set; }
}