using System.Linq.Expressions;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Persistence.Episodes;

/// <summary>
/// Cosmos LINQ predicates for Bluesky post state. Prefer these over the computed
/// <see cref="Episode.BlueskyPosted"/> property (JsonIgnore / not queryable) and over
/// null-only checks — Cosmos requires <c>IS_DEFINED</c> combined with value comparison.
/// </summary>
public static class EpisodeBlueskyCosmosFilters
{
    /// <summary>
    /// Episode is Bluesky-posted: legacy <c>bluesky == true</c> or defined non-null <c>blueskyPost</c>.
    /// </summary>
    public static Expression<Func<Episode, bool>> IsBlueskyPosted { get; } =
        e => (e.OldBlueskyPosted.IsDefined() && e.OldBlueskyPosted == true) ||
             (e.BlueskyPost.IsDefined() && e.BlueskyPost != null);

    /// <summary>
    /// Episode is not Bluesky-posted (ready for a new post from a Cosmos selector).
    /// </summary>
    public static Expression<Func<Episode, bool>> IsNotBlueskyPosted { get; } =
        e => (!e.OldBlueskyPosted.IsDefined() || e.OldBlueskyPosted != true) &&
             (!e.BlueskyPost.IsDefined() || e.BlueskyPost == null);
}
