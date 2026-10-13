using System.Collections.Concurrent;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Providers;

/// <summary>
/// Pass cache for the shared catalogue policy's recent-show-feed stage (see docs/catalogue-pagination.md):
/// one <c>GetShowEpisodes</c> call (a single page, never a catalogue walk) per show, market and page size per
/// podcast pass, mirroring <c>CachedApplePodcastService</c>. Failed calls (null) are not cached, and nothing is
/// fetched while <see cref="IndexingContext.SkipSpotifyUrlResolving"/> (the Spotify circuit breaker) is set.
/// </summary>
public sealed class SpotifyRecentShowEpisodesCache(ISpotifyClientWrapper client)
    : ISpotifyRecentShowEpisodesCache, IPodcastPassApiCacheSource
{
    private readonly ConcurrentDictionary<(string ShowId, string Market, int PageSize), IReadOnlyList<SimpleEpisode>> _cache = new();

    public async Task<IReadOnlyList<SimpleEpisode>> GetRecentShowEpisodes(
        string showId,
        string market,
        int pageSize,
        IndexingContext indexingContext,
        CancellationToken cancellationToken = default)
    {
        var key = (showId, market, pageSize);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (indexingContext.SkipSpotifyUrlResolving)
        {
            return [];
        }

        var page = await client.GetShowEpisodes(
            showId,
            new ShowEpisodesRequest { Market = market, Limit = pageSize },
            indexingContext,
            cancellationToken);
        if (page?.Items == null)
        {
            return [];
        }

        IReadOnlyList<SimpleEpisode> episodes = page.Items.Where(x => x != null).Take(pageSize).ToList();
        _cache[key] = episodes;
        return episodes;
    }

    public void ClearPassCache() => _cache.Clear();
}
