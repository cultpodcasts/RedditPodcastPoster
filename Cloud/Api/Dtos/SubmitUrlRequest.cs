using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace Api.Dtos;

public class SubmitUrlRequest
{
    public required Uri Url { get; set; }

    public Guid? PodcastId { get; set; }

    public string? PodcastName { get; set; }

    /// <summary>
    /// Worker-injected streaming metadata from StreamMeta KV after prepare/extract
    /// (<c>submitUsesPrefetchedMetaWhenCached</c>). Azure trusts the authenticated Worker
    /// caller — do not forward SPA-supplied meta on public submit bodies.
    /// </summary>
    public NonPodcastServiceItemMetaData? PrefetchedMeta { get; set; }
}
