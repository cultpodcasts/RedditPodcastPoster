using CommandLine;

namespace EnrichExistingEpisodesFromPodcastServices;

public class EnrichPodcastEpisodesRequest
{
    [Option('r', "released-since", Required = false, HelpText = "Enrich episodes released within this number of days")]
    public int? ReleasedSince { get; set; }

    [Option('y', "skip-youtube-url-enrichment", Required = false, HelpText = "Skip YouTube-Url resolving",
        Default = false)]
    public bool SkipYouTubeUrlResolving { get; set; }

    [Value(0, MetaName = "podcast-id", Required = true, HelpText = "The Id of the podcast to add this episode to")]
    public Guid PodcastId { get; set; }

    [Option('a', "acknowledge-expensive-queries", Required = false, Default = false, HelpText = "Allow expensive queries")]
    public bool AllowExpensiveQueries { get; set; }
}