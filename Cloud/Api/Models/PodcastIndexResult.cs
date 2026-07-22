using Api.Dtos;

namespace Api.Models;

public enum PodcastIndexStatus
{
    Ok,
    NotFound,
    BadRequest,
    Failed
}

public record PodcastIndexResult(
    PodcastIndexStatus Status,
    IndexPodcastResponse? Response = null);
