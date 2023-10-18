using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.PodcastServices;

public record EnrichmentResult(
    Podcast Podcast,
    Episode Episode,
    EnrichmentContext EnrichmentContext);