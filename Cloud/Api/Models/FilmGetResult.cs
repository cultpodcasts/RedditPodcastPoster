using RedditPodcastPoster.Models.Films;

namespace Api.Models;

public enum FilmGetStatus
{
    Found,
    NotFound,
    Conflict,
    Failed
}

public record FilmGetResult(
    FilmGetStatus Status,
    Film? Film = null,
    IReadOnlyList<Guid>? AmbiguousIds = null);
