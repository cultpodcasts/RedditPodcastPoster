namespace RedditPodcastPoster.UrlSubmission.Models;

public static class UrlMembershipLookupKinds
{
    public const string PodcastService = "podcast-service";
    public const string Streaming = "streaming";
    public const string Unrecognised = "unrecognised";
}

public record UrlMembershipLookupResult(
    bool Known,
    string? Kind = null,
    Guid? PodcastId = null,
    string? PodcastName = null,
    bool Ambiguous = false,
    IReadOnlyList<Guid>? PodcastIds = null,
    /// <summary>
    /// Streaming <see cref="RedditPodcastPoster.Models.Podcasts.ServiceKeys"/> wire value when
    /// <see cref="Kind"/> is streaming; null for podcast-service / unrecognised.
    /// </summary>
    string? Service = null,
    /// <summary>
    /// Catalogue playable kind when submit content types are enabled. Omitted when the flag is off.
    /// </summary>
    string? ContentKind = null,
    /// <summary>
    /// Series or organisation name. Null for Film, which has no parent.
    /// </summary>
    string? ParentName = null,
    /// <summary>
    /// True when submit would reject this URL (e.g. a BBC <c>/news/</c> page, #1211 G5).
    /// </summary>
    bool Rejected = false,
    /// <summary>
    /// Why the URL is rejected, for the submitter. Null when not rejected.
    /// </summary>
    string? RejectionMessage = null);
