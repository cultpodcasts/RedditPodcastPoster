namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Shared mutation for permanent X form failures on <see cref="IPromotable"/> items.
/// </summary>
public static class PromotableTweetSkip
{
    /// <summary>
    /// Records that the item cannot form an X post. Sets <see cref="IPromotable.TweetSkipReason"/> only.
    /// Does not set <see cref="IPromotable.OldTweeted"/> — that legacy flag means posted or duplicate-forbidden.
    /// <see cref="IPromotable.Tweeted"/> already treats a skip reason as done for hourly selection.
    /// </summary>
    public static void ApplyNotTweetable(IPromotable target, string reason)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        target.TweetSkipReason = reason;
    }
}
