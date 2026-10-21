using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace Api.Models;

public enum SubmitUrlPrepareStatus
{
    Ok,
    BadRequest,
    Failed
}

public record SubmitUrlPrepareResult(
    SubmitUrlPrepareStatus Status,
    StreamingService? Service = null,
    NonPodcastServiceItemMetaData? Meta = null,
    string? Message = null);
