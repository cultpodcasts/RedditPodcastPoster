using RedditPodcastPoster.PodcastServices.Abstractions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Client;

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

    Task<IList<T>?> PaginateAll<T, T1>(
        IPaginatable<T, T1> firstPage,
        Func<T1, IPaginatable<T, T1>> mapper,
        IndexingContext indexingContext,
        IPaginator? paginator = null);

    Task<Paging<SimpleEpisode>?> GetShowEpisodes(
        string showId,
        ShowEpisodesRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default);

    Task<List<SimpleShow>> GetSimpleShows(
        SearchRequest request,
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

    Task<EpisodesResponse?> GetSeveral(
        EpisodesRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default);

    Task<Paging<SimpleEpisode, SearchResponse>?> FindEpisodes(
        SearchRequest request,
        IndexingContext indexingContext,
        CancellationToken cancel = default);
}