using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Enrichers;

public interface ISpotifyPodcastEnricher
{
    Task<bool> AddIdAndUrls(Podcast podcast, IEnumerable<Episode> episodes, IndexingContext indexingContext);
}
