using CommandLine;
using RedditPodcastPoster.Common;

namespace RedditPodcastPoster;

public class ProcessRequest
{
    [Option('e', "refresh-episodes", Required = false, HelpText = "Refresh Episode data", Default = false)]
    public bool RefreshEpisodes { get; set; }

    [Option('y', "skip-youtube", Required = false, HelpText = "Skip YouTube-Url resolving",
        Default = false)]
    public bool SkipYouTube { get; set; }

    [Option('s', "skip-spotify", Required = false, HelpText = "Skip Spotify resolving",
        Default = false)]
    public bool SkipSpotify { get; set; }

    [Option('q', "skip-expensive-queries", Required = false, HelpText = "Skip expensive-queries",
        Default = false)]
    public bool SkipExpensiveQueries { get; set; }


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
                releaseBaseLine = DateTimeHelper.DaysAgo(ReleasedSince.Value);
            }

            return releaseBaseLine;
        }
    }
}