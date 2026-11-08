using RedditPodcastPoster.Models.Catalogue;

namespace Api.Services.Catalogue;

/// <summary>What the caller must still do on the social networks after <see cref="PostStatusApplier.Apply"/>.</summary>
public readonly record struct PostStatusChange(bool UnTweet, bool UnBluesky)
{
    public static readonly PostStatusChange None = new(false, false);
}

/// <summary>
/// Post-status edits copied from <c>EpisodeChangeApplier</c>:
/// <list type="bullet">
/// <item><c>posted</c>: sets the flag (the Reddit live un-post is retired, so there is no side effect).</item>
/// <item><c>tweeted</c>: true sets the flag. true → false asks for the tweet to be deleted and leaves the flag set:
/// <see cref="IPlayableSocialRetraction"/> clears it (and the stored tweet id) only once the tweet is gone, so a live
/// tweet is never forgotten.</item>
/// <item><c>blueskyPosted</c>: true sets <see cref="IPromotable.OldBlueskyPosted"/> (curator/manual mark; no network
/// post). true → false asks for the Bluesky post to be deleted and leaves the state set:
/// <see cref="IPlayableSocialRetraction"/> clears it only once the post is gone.</item>
/// <item><c>unBluesky: true</c> is a backward-compat alias for the Bluesky delete path (same as
/// <c>blueskyPosted: false</c> when currently posted).</item>
/// </list>
/// </summary>
public static class PostStatusApplier
{
    public static PostStatusChange Apply(
        IPromotable item,
        bool? posted,
        bool? tweeted,
        bool? blueskyPosted,
        bool? unBluesky)
    {
        if (posted != null)
        {
            item.Posted = posted.Value;
        }

        var unTweet = false;
        if (tweeted != null)
        {
            if (!tweeted.Value && item.Tweeted)
            {
                unTweet = true;
            }
            else
            {
                item.OldTweeted = tweeted.Value ? true : null;
            }
        }

        var unBlueskyPost = false;
        if (blueskyPosted != null)
        {
            if (!blueskyPosted.Value && item.BlueskyPosted)
            {
                unBlueskyPost = true;
            }
            else
            {
                item.OldBlueskyPosted = blueskyPosted.Value ? true : null;
            }
        }
        else if (unBluesky == true && item.BlueskyPosted)
        {
            unBlueskyPost = true;
        }

        return new PostStatusChange(unTweet, unBlueskyPost);
    }

    /// <summary>
    /// Whether the change asks to delete a live Bluesky post (without mutating the item).
    /// </summary>
    public static bool RequestsUnBluesky(IPromotable item, bool? blueskyPosted, bool? unBluesky) =>
        blueskyPosted != null
            ? !blueskyPosted.Value && item.BlueskyPosted
            : unBluesky == true && item.BlueskyPosted;
}
