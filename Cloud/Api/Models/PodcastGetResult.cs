using Podcast = Api.Dtos.Podcast;

namespace Api.Models;

public enum PodcastGetStatus
{
    Found,
    NotFound,
    Conflict,
    Failed
}

public record PodcastGetResult(
    PodcastGetStatus Status,
    Podcast? Podcast = null,
    IEnumerable<Guid>? AmbiguousPodcasts = null);
