namespace Api.Models;

public enum TvShowEpisodeUpdateStatus
{
    Accepted,
    NotFound,
    BadRequest,
    Conflict,
    Failed
}

public record TvShowEpisodeUpdateResult(
    TvShowEpisodeUpdateStatus Status,
    string? Message = null,
    EpisodeUpdateOutcome? Outcome = null,
    string[]? UnexpectedAdditionalYouTubeIds = null);
