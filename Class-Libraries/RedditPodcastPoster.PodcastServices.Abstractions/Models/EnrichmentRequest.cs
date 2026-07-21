using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public record EnrichmentRequest(Podcast Podcast, IEnumerable<Episode> Episodes, Episode Episode);
