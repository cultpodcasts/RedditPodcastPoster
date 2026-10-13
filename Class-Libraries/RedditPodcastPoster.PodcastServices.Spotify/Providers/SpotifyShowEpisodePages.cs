using System.Collections.Concurrent;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Providers;

/// <inheritdoc cref="ISpotifyShowEpisodePages" />
/// <remarks>
/// Every <c>GetShowEpisodes</c> call goes through <see cref="ICatalogueRequestGuard"/>. Failed or timed-out calls
/// (null) are not cached. Nothing is fetched while <see cref="IndexingContext.SkipSpotifyUrlResolving"/> is set.
/// </remarks>
public sealed class SpotifyShowEpisodePages(ISpotifyClientWrapper client, ICatalogueRequestGuard guard)
    : ISpotifyShowEpisodePages, IPodcastPassApiCacheSource
{
    public const int PageSize = 50;

    private readonly ConcurrentDictionary<(string ShowId, string Market, int Offset, int Limit), Paging<SimpleEpisode>> _cache = new();

    public async Task<CatalogueShowPage<SimpleEpisode>?> GetNewestPage(
        string showId,
        string market,
        int pageIndex,
        IndexingContext indexingContext,
        CancellationToken cancellationToken = default)
    {
        if (indexingContext.SkipSpotifyUrlResolving || pageIndex < 0)
        {
            return null;
        }

        var head = await Fetch(showId, market, 0, PageSize, indexingContext, cancellationToken);
        if (head?.Items == null)
        {
            return null;
        }

        var total = head.Total;
        var headItems = head.Items.Where(x => x != null).ToList();
        if (IsNewestFirst(headItems) || total is not { } count)
        {
            var offset = pageIndex * PageSize;
            var page = pageIndex == 0 ? head : await Fetch(showId, market, offset, PageSize, indexingContext, cancellationToken);
            if (page?.Items == null)
            {
                return null;
            }

            var items = page.Items.Where(x => x != null).ToList();
            var hasMore = total is { } t ? offset + items.Count < t : !string.IsNullOrWhiteSpace(page.Next);
            return new CatalogueShowPage<SimpleEpisode>(items, hasMore, total, IsNewestFirst(items));
        }

        // Oldest-first catalogue: page i (newest first) is the slice [total - 50(i+1), total - 50i), reversed.
        var end = count - pageIndex * PageSize;
        if (end <= 0)
        {
            return new CatalogueShowPage<SimpleEpisode>([], false, count);
        }

        var start = Math.Max(0, end - PageSize);
        var slice = start == 0 && end == headItems.Count
            ? head
            : await Fetch(showId, market, start, end - start, indexingContext, cancellationToken);
        if (slice?.Items == null)
        {
            return null;
        }

        var newestFirst = slice.Items.Where(x => x != null).Reverse().ToList();
        return new CatalogueShowPage<SimpleEpisode>(newestFirst, start > 0, count);
    }

    public void ClearPassCache() => _cache.Clear();

    private async Task<Paging<SimpleEpisode>?> Fetch(
        string showId, string market, int offset, int limit, IndexingContext indexingContext, CancellationToken cancellationToken)
    {
        var key = (showId, market, offset, limit);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var page = await guard.Run(
            CataloguePlatforms.Spotify,
            $"GetShowEpisodes show='{showId}' offset={offset}",
            ct => client.GetShowEpisodes(
                showId,
                new ShowEpisodesRequest { Market = market, Limit = limit, Offset = offset },
                indexingContext,
                ct),
            cancellationToken);
        if (page?.Items != null)
        {
            _cache[key] = page;
        }

        return page;
    }

    private static bool IsNewestFirst(IReadOnlyList<SimpleEpisode> items)
    {
        for (var i = 1; i < items.Count; i++)
        {
            if (items[i - 1].GetReleaseDate() < items[i].GetReleaseDate())
            {
                return false;
            }
        }

        return true;
    }
}
