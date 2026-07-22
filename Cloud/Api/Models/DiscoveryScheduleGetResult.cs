using RedditPodcastPoster.Models.Discovery;

namespace Api.Models;

public enum DiscoveryScheduleGetStatus
{
    Ok,
    Failed
}

public record DiscoveryScheduleGetResult(
    DiscoveryScheduleGetStatus Status,
    DiscoveryScheduleConfig? Config = null,
    bool IsDefault = false);
