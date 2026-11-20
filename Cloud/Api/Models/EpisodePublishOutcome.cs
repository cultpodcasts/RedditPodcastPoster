namespace Api.Models;

public class EpisodePublishOutcome(Guid podcastId)
{
    public bool? Posted { get; set; }
    public bool? Tweeted { get; set; }
    public bool? BlueskyPosted { get; set; }
    public string? FailedTweetContent { get; set; }
    public Guid? PodcastId { get; init; } = podcastId;

    /// <summary>Why nothing was published, when the request was refused (e.g. held for approval, #1484).</summary>
    public string? Message { get; set; }

    public bool Updated()
    {
        return (Posted.HasValue && Posted.Value) ||
               (Tweeted.HasValue && Tweeted.Value) ||
               (BlueskyPosted.HasValue && BlueskyPosted.Value);
    }
}
