using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISpotifyUrlCategoriser
{
    Task<ResolvedSpotifyItem> Resolve(Podcast? podcast, Uri url, IndexingContext indexingContext);

    Task<ResolvedSpotifyItem?> Resolve(
        PodcastServiceSearchCriteria criteria,
        Podcast? matchingPodcast,
        IndexingContext indexingContext);

    string GetEpisodeId(Uri url);
}