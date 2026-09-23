namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Promotion / social gating state. Mutating helpers live as extensions on concrete usage
/// (<see cref="PromotableExtensions"/>) — not on this contract.
/// </summary>
public interface IPromotable
{
    bool Ignored { get; set; }

    bool Posted { get; set; }

    bool Tweeted { get; set; }

    /// <summary>Legacy Cosmos flag (<c>bluesky</c>). Prefer <see cref="BlueskyPost"/> for new posts.</summary>
    bool? OldBlueskyPosted { get; set; }

    /// <summary>AT URI of the Bluesky post, when posted.</summary>
    string? BlueskyPost { get; set; }
}
