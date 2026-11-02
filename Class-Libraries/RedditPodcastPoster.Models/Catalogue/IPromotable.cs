namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Promotion / social gating state for catalogue playables.
/// </summary>
public interface IPromotable
{
    bool Ignored { get; set; }

    bool Posted { get; set; }

    /// <summary>Whether an X post is recorded (legacy <c>tweeted</c> flag or a stored <see cref="TweetPost"/>).</summary>
    bool Tweeted { get; }

    /// <summary>Legacy Cosmos flag (<c>tweeted</c>). Prefer <see cref="TweetPost"/> for new posts.</summary>
    bool? OldTweeted { get; set; }

    /// <summary>Id of the X post, when it was stored at post time. Null for older posts.</summary>
    string? TweetPost { get; set; }

    /// <summary>Legacy Cosmos flag (<c>bluesky</c>). Prefer <see cref="BlueskyPost"/> for new posts.</summary>
    bool? OldBlueskyPosted { get; set; }

    /// <summary>AT URI of the Bluesky post, when posted.</summary>
    string? BlueskyPost { get; set; }

    /// <summary>Whether considered Bluesky-posted (legacy flag or stored AT URI).</summary>
    bool BlueskyPosted { get; }

    /// <summary>Optional hashtag appended to Tweet/Bluesky posts (e.g. <c>#MyTag</c>).</summary>
    string? HashTag { get; set; }

    void ClearBlueskyPostState();

    /// <summary>Clears <see cref="OldTweeted"/> and <see cref="TweetPost"/> once the post is gone from X.</summary>
    void ClearTweetState();
}
