using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices;

public record EnrichmentResult(Podcast Podcast, Episode Episode, EnrichmentContext EnrichmentContext);