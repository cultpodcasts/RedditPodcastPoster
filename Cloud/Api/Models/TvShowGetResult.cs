using RedditPodcastPoster.Models.TvShows;

namespace Api.Models;

public enum TvShowGetStatus
{
    Found,
    NotFound,
    Conflict,
    Failed
}

public record TvShowGetResult(
    TvShowGetStatus Status,
    TvShow? TvShow = null,
    IReadOnlyList<Guid>? AmbiguousIds = null);
