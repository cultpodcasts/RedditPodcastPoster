namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Promotion / social gating state for catalogue playables.
/// </summary>
public interface IPromotable
{
    bool Ignored { get; set; }

    bool Posted { get; set; }

    bool Tweeted { get; set; }

    /// <summary>Id of the X post, when it was stored at post time. Null for older posts.</summary>
    string? TweetId { get; set; }

    /// <summary>Legacy Cosmos flag (<c>bluesky</c>). Prefer <see cref="BlueskyPost"/> for new posts.</summary>
    bool? OldBlueskyPosted { get; set; }

    /// <summary>AT URI of the Bluesky post, when posted.</summary>
    string? BlueskyPost { get; set; }

    /// <summary>Whether considered Bluesky-posted (legacy flag or stored AT URI).</summary>
    bool BlueskyPosted { get; }

    /// <summary>Optional hashtag appended to Tweet/Bluesky posts (e.g. <c>#MyTag</c>).</summary>
    string? HashTag { get; set; }

    void ClearBlueskyPostState();

    /// <summary>Clears <see cref="Tweeted"/> and <see cref="TweetId"/> once the post is gone from X.</summary>
    void ClearTweetState();
}
