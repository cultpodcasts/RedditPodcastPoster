using RedditPodcastPoster.Models.Discovery;

namespace Api.Models;

public enum DiscoveryScheduleUpdateStatus
{
    Ok,
    BadRequest,
    Failed
}

public record DiscoveryScheduleUpdateResult(
    DiscoveryScheduleUpdateStatus Status,
    DiscoveryScheduleConfig? Config = null,
    string? Error = null);
