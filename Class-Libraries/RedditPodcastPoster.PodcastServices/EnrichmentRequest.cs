using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices;

public record EnrichmentRequest(Podcast Podcast, Episode Episode);