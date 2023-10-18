using RedditPodcastPoster.Common;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Indexer;

public class IndexerOptions
{
    public int? ReleasedDaysAgo { get; set; }
    public bool ByPassYouTube { get; set; }

    public override string ToString()
    {
        return
            $"{nameof(IndexerOptions)} {{ {nameof(ReleasedDaysAgo)}: '{ReleasedDaysAgo}', {nameof(ByPassYouTube)}: '{ByPassYouTube}'}}.";
    }

    public IndexingContext ToIndexOptions()
    {
        DateTime? releasedSince = null;
        if (ReleasedDaysAgo != null)
        {
            releasedSince = DateTimeHelper.DaysAgo(ReleasedDaysAgo.Value);
        }

        return new IndexingContext(releasedSince, ByPassYouTube);
    }
}