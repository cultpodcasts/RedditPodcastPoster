using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record EnrichmentRequest(Podcast Podcast, IEnumerable<Episode> Episodes, Episode Episode);