using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Models;

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
        context.ExistingEpisode.Release.TimeOfDay == TimeSpan.Zero &&
        context.IncomingEpisode.Release.TimeOfDay > TimeSpan.Zero &&
        DateOnly.FromDateTime(context.ExistingEpisode.Release) ==
        DateOnly.FromDateTime(context.IncomingEpisode.Release);
}
