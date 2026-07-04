using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Merging.Policies;

public sealed class AppleTimeBackfillMergePolicy : IReleaseMergePolicy
{
    public ReleaseMergeOpinion Evaluate(ReleaseMergeContext context)
    {
        if (!YouTubeTimeBackfillMergePolicy.CanBackfillMidnightRelease(context))
        {
            return ReleaseMergeOpinion.NoOpinion;
        }

        return context.IncomingEpisode.AppleId is > 0
            ? ReleaseMergeOpinion.Backfill
            : ReleaseMergeOpinion.NoOpinion;
    }
}
