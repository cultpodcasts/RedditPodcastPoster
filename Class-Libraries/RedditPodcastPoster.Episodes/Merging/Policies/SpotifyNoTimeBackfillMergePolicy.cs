using RedditPodcastPoster.Episodes.Extensions;

namespace RedditPodcastPoster.Episodes.Merging.Policies;

public sealed class SpotifyNoTimeBackfillMergePolicy : IReleaseMergePolicy
{
    public ReleaseMergeOpinion Evaluate(ReleaseMergeContext context)
    {
        if (!YouTubeTimeBackfillMergePolicy.CanBackfillMidnightRelease(context))
        {
            return ReleaseMergeOpinion.NoOpinion;
        }

        if (context.IncomingEpisode.HasSpotifyIdentity() &&
            !context.IncomingEpisode.HasYouTubeOrAppleIdentity())
        {
            return ReleaseMergeOpinion.DoNotBackfill;
        }

        return ReleaseMergeOpinion.NoOpinion;
    }
}
