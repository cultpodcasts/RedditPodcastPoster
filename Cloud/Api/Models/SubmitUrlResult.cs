using Api.Dtos;

namespace Api.Models;

public enum SubmitUrlStatus
{
    Ok,
    PodcastNotFound,
    Failed
}

public record SubmitUrlResult(
    SubmitUrlStatus Status,
    SubmitUrlResponse? Response = null,
    string? Message = null);
