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
/// <see cref="IPlayableSocialRetraction"/> clears it only once the delete succeeds, so a live tweet is never forgotten.</item>
/// <item><c>unBluesky: true</c> on a Bluesky-posted item asks for the post to be deleted. The Bluesky state is not
/// cleared here: <see cref="IPlayableSocialRetraction"/> deletes first and clears only on success.</item>
/// </list>
/// </summary>
public static class PostStatusApplier
{
    public static PostStatusChange Apply(IPromotable item, bool? posted, bool? tweeted, bool? unBluesky)
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
                item.Tweeted = tweeted.Value;
            }
        }

        var unBlueskyPost = unBluesky == true && item.BlueskyPosted;
        return new PostStatusChange(unTweet, unBlueskyPost);
    }
}
