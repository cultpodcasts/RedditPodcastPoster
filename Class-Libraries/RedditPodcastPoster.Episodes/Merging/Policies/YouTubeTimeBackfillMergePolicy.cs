using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Episodes.Merging;

namespace RedditPodcastPoster.Episodes.Merging.Policies;

public sealed class YouTubeTimeBackfillMergePolicy : IReleaseMergePolicy
{
    public ReleaseMergeOpinion Evaluate(ReleaseMergeContext context)
    {
        if (!CanBackfillMidnightRelease(context))
        {
            return ReleaseMergeOpinion.NoOpinion;
        }

        return context.IncomingEpisode.HasYouTubeIdentity()
            ? ReleaseMergeOpinion.Backfill
            : ReleaseMergeOpinion.NoOpinion;
    }

    internal static bool CanBackfillMidnightRelease(ReleaseMergeContext context) =>
        context.ExistingEpisode.ReleaseUtc.TimeOfDay == TimeSpan.Zero &&
        context.IncomingEpisode.ReleaseUtc.TimeOfDay > TimeSpan.Zero &&
        DateOnly.FromDateTime(context.ExistingEpisode.ReleaseUtc) ==
        DateOnly.FromDateTime(context.IncomingEpisode.ReleaseUtc);
}
