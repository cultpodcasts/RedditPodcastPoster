using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Merging;

public enum ReleaseMergeOpinion
{
    NoOpinion,
    Preserve,
    Backfill,
    DoNotBackfill
}

public sealed record ReleaseMergeContext(
    Podcast Podcast,
    Episode ExistingEpisode,
    Episode IncomingEpisode);

public interface IReleaseMergePolicy
{
    ReleaseMergeOpinion Evaluate(ReleaseMergeContext context);
}
