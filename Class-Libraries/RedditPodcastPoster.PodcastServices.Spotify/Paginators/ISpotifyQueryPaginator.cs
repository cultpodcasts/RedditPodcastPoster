using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using SpotifyAPI.Web;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Paginators;

public interface ISpotifyQueryPaginator
{
    Task<PodcastEpisodesResult> PaginateEpisodes(
        IPaginatable<SimpleEpisode>? pagedEpisodes,
        IndexingContext indexingContext);
}
