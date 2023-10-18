using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record EnrichmentRequest(Podcast Podcast, Episode Episode);