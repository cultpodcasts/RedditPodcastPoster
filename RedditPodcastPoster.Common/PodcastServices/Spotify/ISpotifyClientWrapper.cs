using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ISpotifyClientWrapper
{
    Task<IList<T>?> Paginate<T>(
        IPaginatable<T> firstPage,
        IndexingContext indexingContext,
        IPaginator? paginator = null,
        CancellationToken cancel = default);

    Task<IList<T>?> PaginateAll<T>(
        IPaginatable<T> firstPage,
        IndexingContext indexingContext,
        IPaginator? paginator = null);

    Task<Paging<SimpleEpisode>?> GetShowEpisodes(
        string showId,
        ShowEpisodesRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default);

    Task<SearchResponse?> GetSearchResponse(
        SearchRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default);

    Task<FullShow?> GetFullShow(
        string showId, 
        ShowRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default);

    Task<FullEpisode?> GetFullEpisode(
        string episodeId, 
        EpisodeRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default);
}