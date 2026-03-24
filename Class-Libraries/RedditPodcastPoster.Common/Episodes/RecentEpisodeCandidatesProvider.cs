using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Persistence.Abstractions;
using Episode = RedditPodcastPoster.Models.V2.Episode;

namespace RedditPodcastPoster.Common.Episodes;

public class RecentEpisodeCandidatesProvider(
    IEpisodeRepository episodeRepository,
    IPodcastRepositoryV2 podcastRepository,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<RecentEpisodeCandidatesProvider> logger)
    : IRecentEpisodeCandidatesProvider
{
    private static readonly SemaphoreSlim CacheLock = new(1, 1);

    private readonly DateTime _cacheReleasedSince = DateOnly
        .FromDateTime(DateTime.UtcNow)
        .AddDays(postingCriteria.Value.MaxDays * -1)
        .ToDateTime(TimeOnly.MinValue);

    private IReadOnlyCollection<Episode>? _cachedEpisodes;

    public async Task<IReadOnlyCollection<Episode>> GetRecentActiveEpisodes(DateTime releasedSince)
    {
        if (TryGetCachedEpisodes(releasedSince, out var cachedEpisodes))
        {
            logger.LogInformation(
                "Using cached recent episode candidates. Requested released-since: '{ReleasedSince:O}', Cached released-since: '{CachedReleasedSince:O}', Count: {Count}.",
                releasedSince,
                _cacheReleasedSince,
                cachedEpisodes.Count);
            return cachedEpisodes;
        }

        await CacheLock.WaitAsync();
        try
        {
            if (TryGetCachedEpisodes(releasedSince, out cachedEpisodes))
            {
                logger.LogInformation(
                    "Using cached recent episode candidates after lock. Requested released-since: '{ReleasedSince:O}', Cached released-since: '{CachedReleasedSince:O}', Count: {Count}.",
                    releasedSince,
                    _cacheReleasedSince,
                    cachedEpisodes.Count);
                return cachedEpisodes;
            }

            if (releasedSince < _cacheReleasedSince)
            {
                logger.LogError(
                    "Requested released-since '{ReleasedSince:O}' is older than cache window '{CacheReleasedSince:O}'. Returning cache-window results.",
                    releasedSince,
                    _cacheReleasedSince);
            }

            _cachedEpisodes = await LoadRecentEpisodes(_cacheReleasedSince);

            var requestedEpisodes = releasedSince <= _cacheReleasedSince
                ? _cachedEpisodes
                : _cachedEpisodes.Where(x => x.Release >= releasedSince).ToArray();

            logger.LogInformation(
                "Loaded recent episode candidates via latestReleased-scoped partition reads. Requested released-since: '{ReleasedSince:O}', Cache released-since: '{CacheReleasedSince:O}', Count: {Count}.",
                releasedSince,
                _cacheReleasedSince,
                requestedEpisodes.Count);

            return requestedEpisodes;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private bool TryGetCachedEpisodes(
        DateTime releasedSince,
        out IReadOnlyCollection<Episode> episodes)
    {
        episodes = [];
        if (_cachedEpisodes == null || releasedSince < _cacheReleasedSince)
        {
            return false;
        }

        episodes = releasedSince == _cacheReleasedSince
            ? _cachedEpisodes
            : _cachedEpisodes.Where(x => x.Release >= releasedSince).ToArray();

        return true;
    }

    private async Task<IReadOnlyCollection<Episode>> LoadRecentEpisodes(DateTime releasedSince)
    {
        var recentPodcastIds = await podcastRepository
            .GetAllBy(
                x => (!x.Removed.IsDefined() || x.Removed == false) &&
                     x.LatestReleased.IsDefined() &&
                     x.LatestReleased != null &&
                     x.LatestReleased >= releasedSince,
                x => x.Id)
            .ToArrayAsync();

        if (recentPodcastIds.Length == 0)
        {
            logger.LogInformation(
                "No recently active podcasts found for candidate retrieval. Released-since: '{ReleasedSince:O}'.",
                releasedSince);
            return [];
        }

        var episodes = new List<Episode>();
        foreach (var podcastId in recentPodcastIds)
        {
            var podcastEpisodes = await episodeRepository
                .GetByPodcastId(podcastId, x => x.Release >= releasedSince && !x.Ignored && !x.Removed)
                .ToArrayAsync();

            episodes.AddRange(podcastEpisodes);
        }

        return episodes
            .OrderByDescending(x => x.Release)
            .ToArray();
    }
}