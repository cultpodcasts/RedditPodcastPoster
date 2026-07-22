using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace Api.Models;

public enum SubjectGetStatus
{
    Ok,
    NotFound,
    Failed
}

public record SubjectGetResult(
    SubjectGetStatus Status,
    Subject? Subject = null);
