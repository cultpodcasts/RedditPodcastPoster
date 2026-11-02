using System.Linq.Expressions;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Persistence.Episodes;

/// <summary>
/// Cosmos LINQ predicates for X post state. Prefer these over the computed
/// <see cref="Episode.Tweeted"/> property (JsonIgnore / not queryable) and over
/// null-only or <c>tweeted</c>-alone checks — Cosmos requires <c>IS_DEFINED</c>
/// combined with value comparison, and <c>tweetPost</c>-only documents must count as tweeted.
/// </summary>
public static class EpisodeTweetCosmosFilters
{
    /// <summary>
    /// Episode is tweeted: legacy <c>tweeted == true</c> or defined non-null <c>tweetPost</c>.
    /// </summary>
    public static Expression<Func<Episode, bool>> IsTweeted { get; } =
        e => (e.OldTweeted.IsDefined() && e.OldTweeted == true) ||
             (e.TweetPost.IsDefined() && e.TweetPost != null);

    /// <summary>
    /// Episode is not tweeted (ready for a new post from a Cosmos selector).
    /// </summary>
    public static Expression<Func<Episode, bool>> IsNotTweeted { get; } =
        e => (!e.OldTweeted.IsDefined() || e.OldTweeted != true) &&
             (!e.TweetPost.IsDefined() || e.TweetPost == null);
}
