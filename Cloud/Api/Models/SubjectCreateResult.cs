using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace Api.Models;

public enum SubjectCreateStatus
{
    Accepted,
    BadRequest,
    Conflict,
    Failed
}

public record SubjectCreateResult(
    SubjectCreateStatus Status,
    Subject? Subject = null,
    string? ConflictName = null,
    string? Message = null);
