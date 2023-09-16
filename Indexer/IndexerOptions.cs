using RedditPodcastPoster.Common;

namespace Indexer;

public class IndexerOptions
{
    public int? ReleasedDaysAgo { get; set; }
    public bool ByPassYouTube { get; set; }

    public override string ToString()
    {
        return
            $"IndexerOptions {{ {nameof(ReleasedDaysAgo)}: '{ReleasedDaysAgo}', {nameof(ByPassYouTube)}: '{ByPassYouTube}'}}.";
    }

    public IndexOptions ToIndexOptions()
    {
        DateTime? releasedSince = null;
        if (ReleasedDaysAgo != null)
        {
            releasedSince= DateTimeHelper.DaysAgo(ReleasedDaysAgo.Value);
        }

        return new IndexOptions(releasedSince, ByPassYouTube);
    }

}