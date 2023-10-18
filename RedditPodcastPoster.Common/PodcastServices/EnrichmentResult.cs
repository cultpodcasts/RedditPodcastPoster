using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices;

namespace RedditPodcastPoster.Common.PodcastServices;

public record EnrichmentResult(Podcast Podcast, Episode Episode, EnrichmentContext EnrichmentContext);