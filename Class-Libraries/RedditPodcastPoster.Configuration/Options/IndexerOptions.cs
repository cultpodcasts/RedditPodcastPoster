using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Configuration.Options;

public class IndexerOptions
{
    public int? ReleasedDaysAgo { get; set; }
    public bool ByPassYouTube { get; set; }

    /// <summary>
    /// App setting <c>indexer__ParallelPasses</c>. Absent binds as <see cref="ParallelPassesMode.Off"/>.
    /// Read only from the <c>IndexIdProvider</c> activity — orchestrators must not read configuration.
    /// </summary>
    public ParallelPassesMode ParallelPasses { get; set; } = ParallelPassesMode.Off;

    public override string ToString()
    {
        return
            $"{nameof(IndexerOptions)} released-days-ago: '{ReleasedDaysAgo}', bypass-youtube: '{ByPassYouTube}', parallel-passes: '{ParallelPasses}'.";
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
