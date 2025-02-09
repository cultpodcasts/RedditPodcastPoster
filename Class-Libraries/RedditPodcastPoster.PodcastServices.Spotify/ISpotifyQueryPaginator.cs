using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public interface ISpotifyQueryPaginator
{
    Task<PodcastEpisodesResult> PaginateEpisodes(
        IPaginatable<SimpleEpisode>? pagedEpisodes,
        IndexingContext indexingContext);
}