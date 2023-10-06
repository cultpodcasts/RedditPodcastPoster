using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ICachedSpotifyClient : IFlushable
{
    CachedSpotifyClient.CachedEpisodesClient Episodes { get; }
    CachedSpotifyClient.CachedShowsClient Shows { get; }
    CachedSpotifyClient.CachedSearchClient Search { get; }

    Task<IList<SimpleEpisode>?> PaginateAll(
        IPaginatable<SimpleEpisode> firstPage,
        string cacheKey,
        IndexingContext indexingContext,
        IPaginator? paginator = null);

    Task<IList<SimpleEpisode>?> Paginate(
        IPaginatable<SimpleEpisode> firstPage,
        string cacheKey,
        IndexingContext indexingContext,
        IPaginator? paginator = null,
        CancellationToken cancel = default);
}