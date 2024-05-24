using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record EnrichmentResult(
    Podcast Podcast,
    Episode Episode,
    EnrichmentContext EnrichmentContext);