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
    IReadOnlyList<Guid>? PodcastIds = null);
