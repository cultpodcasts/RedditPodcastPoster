using RedditPodcastPoster.UrlSubmission.Models;

namespace Api.Models;

public enum SubmitUrlStatus
{
    Ok,
    PodcastNotFound,
    Failed
}

public record SubmitUrlResult(
    SubmitUrlStatus Status,
    SubmitResult? Result = null,
    string? Message = null);
