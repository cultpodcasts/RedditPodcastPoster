using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public interface ICachedSpotifyClient: IFlushable
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

public interface IFlushable
{
    void Flush();
}

public class CacheFlusher : IFlushable
{
    private readonly ICachedSpotifyClient _cachedSpotifyClient;
    private readonly ICachedApplePodcastService _cachedApplePodcastService;
    private readonly ILogger<CacheFlusher> _logger;

    public CacheFlusher(
        ICachedSpotifyClient cachedSpotifyClient,
        ICachedApplePodcastService cachedApplePodcastService,   
        ILogger<CacheFlusher> logger)
    {
        _cachedSpotifyClient = cachedSpotifyClient;
        _cachedApplePodcastService = cachedApplePodcastService;
        _logger = logger;
    }

    public void Flush()
    {
        _logger.LogInformation($"{nameof(Flush)}");
        _cachedApplePodcastService.Flush();
        _cachedSpotifyClient.Flush();
    }
}