using CommandLine;

namespace RedditPodcastPoster.Common;

public class ProcessRequest
{
    [Option('e', "refresh-episodes", Required = false, HelpText = "Refresh Episode data", Default = false)]
    public bool RefreshEpisodes { get; set; }

    [Option('y', "skip-youtube-url-enrichment", Required = false, HelpText = "Skip YouTube-Url resolving",
        Default = false)]
    public bool SkipYouTubeUrlResolving { get; set; }

    [Value(0, MetaName = "release-within-days",
        HelpText = "Unposted podcasts released within these number of days will be posted")]
    public int? ReleasedSince { get; set; }

    public DateTime? ReleaseBaseline
    {
        get
        {
            DateTime? releaseBaseLine = null;
            if (ReleasedSince != null)
            {
                releaseBaseLine = DateOnly
                    .FromDateTime(DateTime.UtcNow)
                    .AddDays(ReleasedSince.Value * -1)
                    .ToDateTime(TimeOnly.MinValue);
            }

            return releaseBaseLine;
        }
    }
}