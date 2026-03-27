using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record EnrichmentRequest(Podcast Podcast, IEnumerable<Episode> Episodes, Episode Episode);