using RedditPodcastPoster.Episodes.Merging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Merging.Policies;

/// <summary>
/// When Apple is release authority, adopt the incoming Apple publish datetime even if the
/// existing episode already has a timed YouTube release on another calendar day
/// (midnight same-day backfill alone is not enough).
/// </summary>
public sealed class AppleAuthoritativeAdoptReleaseMergePolicy : IReleaseMergePolicy
{
    public ReleaseMergeOpinion Evaluate(ReleaseMergeContext context)
    {
        if (context.Podcast.ReleaseAuthority != Service.Apple)
        {
            return ReleaseMergeOpinion.NoOpinion;
        }

        if (EpisodeServicePresence.AppleEpisodeId(context.IncomingEpisode) is null or 0)
        {
            return ReleaseMergeOpinion.NoOpinion;
        }

        if (context.IncomingEpisode.Release == context.ExistingEpisode.Release)
        {
            return ReleaseMergeOpinion.NoOpinion;
        }

        return ReleaseMergeOpinion.Backfill;
    }
}
