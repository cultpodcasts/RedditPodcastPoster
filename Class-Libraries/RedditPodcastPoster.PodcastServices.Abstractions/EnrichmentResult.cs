using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record EnrichmentResult(
    Podcast Podcast,
    Episode Episode,
    EnrichmentContext EnrichmentContext);