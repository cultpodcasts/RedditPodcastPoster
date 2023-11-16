using CommandLine;

namespace CategorisePodcastEpisodes;

public class CategorisePodcastEpisodesRequest

{
    [Value(0, MetaName = "podcast-ids", Required = true,
        HelpText = "The Ids of the podcast to categorised, comma-separated")]
    public string PodcastIds { get; set; }

    [Option('c', "Commit", Default = false, Required = false)]
    public bool Commit { get; set; }
}