namespace Api.Models;

public class EpisodePublishOutcome(Guid podcastId)
{
    public bool? Posted { get; set; }
    public bool? Tweeted { get; set; }
    public bool? BlueskyPosted { get; set; }
    public string? FailedTweetContent { get; set; }
    public Guid? PodcastId { get; init; } = podcastId;

    public bool Updated()
    {
        return (Posted.HasValue && Posted.Value) ||
               (Tweeted.HasValue && Tweeted.Value) ||
               (BlueskyPosted.HasValue && BlueskyPosted.Value);
    }
}
