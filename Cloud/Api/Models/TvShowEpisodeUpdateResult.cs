namespace Api.Models;

public enum TvShowEpisodeUpdateStatus
{
    Accepted,
    NotFound,
    BadRequest,
    Failed
}

public record TvShowEpisodeUpdateResult(
    TvShowEpisodeUpdateStatus Status,
    string? Message = null);
