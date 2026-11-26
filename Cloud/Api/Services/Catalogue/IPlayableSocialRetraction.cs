using RedditPodcastPoster.Models.Catalogue;

namespace Api.Services.Catalogue;

/// <summary>Deletes a news report's, TV episode's or film's tweet / Bluesky post, the way <c>EpisodeUpdateService</c> does.</summary>
public interface IPlayableSocialRetraction
{
    /// <summary>
    /// Clears the Bluesky state only when the post was deleted or is already gone. Callers on an etag loop do this
    /// after the replace commits, then patch the flags. Returns null when nothing was requested.
    /// </summary>
    Task<bool?> RemoveBlueskyPost(PostStatusChange change, IPromotable item, SocialPostRemovalTarget target);

    /// <summary>
    /// Clears <c>tweeted</c> only when the tweet was deleted; not found or a failure leaves it set so the un-tweet
    /// can be retried. Callers on an etag loop do this after the replace commits, then patch the flags.
    /// Returns null when nothing was requested.
    /// </summary>
    Task<bool?> RemoveTweet(PostStatusChange change, IPromotable item, SocialPostRemovalTarget target);
}
