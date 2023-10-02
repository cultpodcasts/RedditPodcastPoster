using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices;

public record EnrichmentRequest(Podcast Podcast, Episode Episode);