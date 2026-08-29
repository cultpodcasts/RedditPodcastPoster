using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Episodes.Merging;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Merging.Policies;

public sealed class AppleTimeBackfillMergePolicy : IReleaseMergePolicy
{
    public ReleaseMergeOpinion Evaluate(ReleaseMergeContext context)
    {
        if (!YouTubeTimeBackfillMergePolicy.CanBackfillMidnightRelease(context))
        {
            return ReleaseMergeOpinion.NoOpinion;
        }

        return EpisodeServicePresence.AppleEpisodeId(context.IncomingEpisode) is > 0
            ? ReleaseMergeOpinion.Backfill
            : ReleaseMergeOpinion.NoOpinion;
    }
}
