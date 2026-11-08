namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Promotion / social gating state for catalogue playables.
/// </summary>
public interface IPromotable
{
    bool Ignored { get; set; }

    bool Posted { get; set; }

    /// <summary>
    /// Whether treated as done for X posting: stored <see cref="TweetPost"/>, legacy <c>tweeted</c>
    /// (<see cref="OldTweeted"/>), or a permanent <see cref="TweetSkipReason"/>.
    /// </summary>
    bool Tweeted { get; }

    /// <summary>Legacy Cosmos flag (<c>tweeted</c>). Prefer <see cref="TweetPost"/> for new posts.</summary>
    bool? OldTweeted { get; set; }

    /// <summary>Id of the X post, when it was stored at post time. Null for older posts.</summary>
    string? TweetPost { get; set; }

    /// <summary>
    /// Why this item must not be posted to X (e.g. title budget exhausted). When set, treat as done for
    /// posting so the hourly run does not retry or rewrite the shortener. Cleared with <see cref="ClearTweetState"/>.
    /// </summary>
    string? TweetSkipReason { get; set; }

    /// <summary>
    /// Legacy Cosmos flag (<c>bluesky</c>). Curator/manual mark via the change API may set this true;
    /// a real network post should store <see cref="BlueskyPost"/> instead.
    /// </summary>
    bool? OldBlueskyPosted { get; set; }

    /// <summary>AT URI of the Bluesky post, when posted.</summary>
    string? BlueskyPost { get; set; }

    /// <summary>Whether considered Bluesky-posted (legacy flag or stored AT URI).</summary>
    bool BlueskyPosted { get; }

    /// <summary>Optional hashtag appended to Tweet/Bluesky posts (e.g. <c>#MyTag</c>).</summary>
    string? HashTag { get; set; }

    void ClearBlueskyPostState();

    /// <summary>
    /// Clears <see cref="OldTweeted"/>, <see cref="TweetPost"/>, and <see cref="TweetSkipReason"/>
    /// once the post is gone from X or the skip must be undone.
    /// </summary>
    void ClearTweetState();
}
