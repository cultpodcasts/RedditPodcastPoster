using RedditPodcastPoster.UrlSubmission.Models;

namespace Api.Models;

public enum SubmitUrlStatus
{
    Ok,
    PodcastNotFound,
    Conflict,
    Failed,
    Rejected,
    RequiresCurator
}

public record SubmitUrlResult(
    SubmitUrlStatus Status,
    SubmitResult? Result = null,
    string? Message = null,
    IEnumerable<Guid>? AmbiguousPodcasts = null,
    string? ContentKind = null,
    string? ParentName = null);
