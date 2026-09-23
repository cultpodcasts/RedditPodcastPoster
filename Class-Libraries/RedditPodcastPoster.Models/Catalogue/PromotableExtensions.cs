namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Query/mutate helpers for <see cref="IPromotable"/>. Kept off the interface so the
/// contract stays data-only.
/// </summary>
public static class PromotableExtensions
{
    public static bool IsBlueskyPosted(this IPromotable promotable) =>
        (promotable.OldBlueskyPosted == true) || !string.IsNullOrWhiteSpace(promotable.BlueskyPost);

    public static void ClearBlueskyPostState(this IPromotable promotable)
    {
        promotable.BlueskyPost = null;
        promotable.OldBlueskyPosted = null;
    }
}
