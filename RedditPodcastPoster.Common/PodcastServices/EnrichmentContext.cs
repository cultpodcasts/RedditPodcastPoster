using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices;

public record EnrichmentContext
{
    public bool Updated { get; set; }

    public Uri? YouTube { get; set; } = null;
    public Uri? Spotify { get; set; } = null;
    public Uri? Apple { get; set; } = null;

    public EnrichmentResult ToEnrichmentResult()
    {
        return new EnrichmentResult(Updated);
    }
}