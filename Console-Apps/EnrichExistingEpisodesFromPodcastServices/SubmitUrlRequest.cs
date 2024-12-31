using CommandLine;

namespace EnrichExistingEpisodesFromPodcastServices;

public class EnrichPodcastEpisodesRequest
{
    [Option('r', "released-since", Required = false, HelpText = "Enrich episodes released within this number of days")]
    public int? ReleasedSince { get; set; }

    [Option('y', "skip-youtube-url-enrichment", Required = false, HelpText = "Skip YouTube-Url resolving",
        Default = false)]
    public bool SkipYouTubeUrlResolving { get; set; }

    [Option('p', "podcast-id", Required = false, HelpText = "The Id of the podcast to add this episode to")]
    public Guid? PodcastId { get; set; }

    [Option('n', "podcast-name", Required = false, HelpText = "The name of the podcast to index")]
    public string? PodcastName { get; set; }

    [Option('a', "acknowledge-expensive-queries", Required = false, Default = false, HelpText = "Allow expensive queries")]
    public bool AllowExpensiveQueries { get; set; }
}