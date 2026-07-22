using Api.Dtos;

namespace Api.Models;

public enum HomepagePublishStatus
{
    Ok,
    Failed
}

public record HomepagePublishResult(
    HomepagePublishStatus Status,
    PublishHomepageResponse? Response = null);
