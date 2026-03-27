using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class RecentEpisodeCandidatesProvider(
    IEpisodeRepository episodeRepository,
    IPodcastRepository podcastRepository,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<RecentEpisodeCandidatesProvider> logger)
    : IRecentEpisodeCandidatesProvider
{
    private static readonly SemaphoreSlim CacheLock = new(1, 1);

    private readonly DateTime _cacheReleasedSince = DateOnly
        .FromDateTime(DateTime.UtcNow)
        .AddDays(postingCriteria.Value.MaxDays * -1)
        .ToDateTime(TimeOnly.MinValue);

    private IReadOnlyCollection<PodcastEpisode>? _cachedEpisodes;

    /// <summary>
    /// Gets all recent episodes from cache, including ignored and removed ones.
    /// Handles cache initialization and retrieval without filtering.
    /// </summary>
    public async Task<IReadOnlyCollection<PodcastEpisode>> GetEpisodes(DateTime releasedSince)
    {
        if (TryGetCachedEpisodes(releasedSince, out var cachedEpisodes))
        {
            logger.LogInformation(
                "Using cached recent episodes. Requested released-since: '{ReleasedSince:O}', Cached released-since: '{CachedReleasedSince:O}', Count: {Count}.",
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
                    "Using cached recent episodes after lock. Requested released-since: '{ReleasedSince:O}', Cached released-since: '{CachedReleasedSince:O}', Count: {Count}.",
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

            var podcastEpisodes = await LoadRecentPodcastEpisodes(_cacheReleasedSince);
            _cachedEpisodes = podcastEpisodes;

            var requestedEpisodes = releasedSince <= _cacheReleasedSince
                ? _cachedEpisodes
                : _cachedEpisodes.Where(x => x.Episode.Release >= releasedSince).ToArray();

            logger.LogInformation(
                "Loaded recent episodes via latestReleased-scoped partition reads. Requested released-since: '{ReleasedSince:O}', Cache released-since: '{CachedReleasedSince:O}', Count: {Count}.",
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

    /// <summary>
    /// Gets recent active episodes, filtering out ignored and removed ones.
    /// </summary>
    public async Task<IList<PodcastEpisode>> GetRecentActiveEpisodes(DateTime releasedSince)
    {
        var episodes = await GetEpisodes(releasedSince);
        return episodes
            .Where(x => x.Episode is { Ignored: false, Removed: false })
            .ToList();
    }

    private bool TryGetCachedEpisodes(
        DateTime releasedSince,
        out IReadOnlyCollection<PodcastEpisode> episodes)
    {
        episodes = [];
        if (_cachedEpisodes == null || releasedSince < _cacheReleasedSince)
        {
            return false;
        }

        episodes = releasedSince == _cacheReleasedSince
            ? _cachedEpisodes
            : _cachedEpisodes.Where(x => x.Episode.Release >= releasedSince).ToArray();

        return true;
    }

    private async Task<IReadOnlyCollection<PodcastEpisode>> LoadRecentPodcastEpisodes(DateTime releasedSince)
    {
        var recentPodcasts = await podcastRepository
            .GetAllBy(x => (!x.Removed.IsDefined() || x.Removed == false) &&
                           x.LatestReleased.IsDefined() &&
                           x.LatestReleased != null &&
                           x.LatestReleased >= releasedSince)
            .ToArrayAsync();

        if (recentPodcasts.Length == 0)
        {
            logger.LogInformation(
                "No recently active podcasts found for candidate retrieval. Released-since: '{ReleasedSince:O}'.",
                releasedSince);
            return [];
        }

        var podcastEpisodes = new List<PodcastEpisode>();
        foreach (var podcast in recentPodcasts)
        {
            var episodes = await episodeRepository
                .GetByPodcastId(podcast.Id, x => x.Release >= releasedSince)
                .ToArrayAsync();

            foreach (var episode in episodes)
            {
                podcastEpisodes.Add(new PodcastEpisode(podcast, episode));
            }
        }

        return podcastEpisodes
            .OrderByDescending(x => x.Episode.Release)
            .ToArray();
    }
}