using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Configuration;

public class IndexerOptions
{
    public int? ReleasedDaysAgo { get; set; }
    public bool ByPassYouTube { get; set; }

    public override string ToString()
    {
        return
            $"{nameof(IndexerOptions)} released-days-ago: '{ReleasedDaysAgo}', bypass-youtube: '{ByPassYouTube}'.";
    }

    public IndexingContext ToIndexingContext()
    {
        DateTime? releasedSince = null;
        if (ReleasedDaysAgo != null)
        {
            releasedSince = DateTimeExtensions.DaysAgo(ReleasedDaysAgo.Value);
        }

        return new IndexingContext(releasedSince, SkipYouTubeUrlResolving: ByPassYouTube);
    }
}