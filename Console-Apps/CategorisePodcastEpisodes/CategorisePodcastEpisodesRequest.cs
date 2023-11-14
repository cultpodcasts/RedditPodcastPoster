using CommandLine;

namespace CategorisePodcastEpisodes;

public class CategorisePodcastEpisodesRequest

{
    [Value(0, MetaName = "podcast-id", Required = true, HelpText = "The Id of the podcast to add this episode to")]
    public Guid PodcastId { get; set; }

    [Option('c', longName:"Commit", Default = false, Required = false)]
    public bool Commit { get; set; }
}