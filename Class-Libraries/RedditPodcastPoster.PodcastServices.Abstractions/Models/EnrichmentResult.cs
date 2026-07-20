using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public record EnrichmentResult(
    Podcast Podcast,
    Episode Episode,
    EnrichmentContext EnrichmentContext);