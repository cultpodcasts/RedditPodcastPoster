using Person = RedditPodcastPoster.Models.People.Person;

namespace Api.Models;

public enum PersonGetStatus
{
    Ok,
    NotFound,
    Failed
}

public record PersonGetResult(
    PersonGetStatus Status,
    Person? Person = null);
