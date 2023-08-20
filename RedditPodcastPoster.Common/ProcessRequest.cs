using CommandLine;

namespace RedditPodcastPoster.Common;

public class ProcessRequest
{
    [Option('e', "refresh-episodes", Required = false, HelpText = "Refresh Episode data", Default = false)]
    public bool RefreshEpisodes { get; set; }

    [Option('y', "skip-youtube-url-enrichment", Required = false, HelpText = "Skip YouTube-Url resolving",
        Default = false)]
    public bool SkipYouTubeUrlResolving { get; set; }

    [Value(0, MetaName = "released-since", HelpText = "Set date-time since last set of Reddit-posts")]
    public DateTime? ReleasedSince { get; set; }

}