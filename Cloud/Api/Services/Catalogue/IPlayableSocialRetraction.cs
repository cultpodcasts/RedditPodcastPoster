using RedditPodcastPoster.Models.Catalogue;

namespace Api.Services.Catalogue;

/// <summary>Deletes a news report's, TV episode's or film's tweet / Bluesky post, the way <c>EpisodeUpdateService</c> does.</summary>
public interface IPlayableSocialRetraction
{
    /// <summary>
    /// Call before saving, while the AT URI is still on the item. Clears the Bluesky state only when the post was
    /// deleted or is already gone. Returns null when nothing was requested.
    /// </summary>
    Task<bool?> RemoveBlueskyPost(PostStatusChange change, IPromotable item, SocialPostRemovalTarget target);

    /// <summary>
    /// Call before saving. Clears <c>tweeted</c> only when the tweet was deleted; not found or a failure leaves it set
    /// so the un-tweet can be retried. Returns null when nothing was requested.
    /// </summary>
    Task<bool?> RemoveTweet(PostStatusChange change, IPromotable item, SocialPostRemovalTarget target);
}
