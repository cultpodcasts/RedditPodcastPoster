using RedditPodcastPoster.UrlSubmission.Models;

namespace Api.Models;

public enum SubmitUrlStatus
{
    Ok,
    PodcastNotFound,
    Conflict,
    Failed,
    Rejected,
    RequiresCurator,
    /// <summary>The request itself is invalid (e.g. parent and podcast fields together); 400 with a message.</summary>
    BadRequest,
    /// <summary>The <c>parentId</c> is not a row of <c>parentKind</c>; 404.</summary>
    ParentNotFound,

    /// <summary>Detected language is not supported yet; nothing was created. 200 with <c>languageDecision</c>.</summary>
    LanguageDecisionRequired,

    /// <summary>The caller chose cancel; nothing was created.</summary>
    Cancelled
}

public record SubmitUrlResult(
    SubmitUrlStatus Status,
    SubmitResult? Result = null,
    string? Message = null,
    IEnumerable<Guid>? AmbiguousPodcasts = null,
    string? ContentKind = null,
    string? ParentName = null);
