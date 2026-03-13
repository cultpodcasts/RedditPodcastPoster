using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Categorisers;

public interface ISpotifyUrlCategoriser
{
    Task<ResolvedSpotifyItem> Resolve(Podcast? podcast, IEnumerable<Episode> episodes, Uri url,
        IndexingContext indexingContext);

    Task<ResolvedSpotifyItem?> Resolve(
        PodcastServiceSearchCriteria criteria,
        Podcast? matchingPodcast,
        IndexingContext indexingContext);
}