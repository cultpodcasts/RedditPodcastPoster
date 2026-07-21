using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public record EnrichmentResult(
    Podcast Podcast,
    Episode Episode,
    EnrichmentContext EnrichmentContext);