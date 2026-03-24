using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using Episode = RedditPodcastPoster.Models.V2.Episode;

namespace RedditPodcastPoster.Common.Episodes;

public class RecentEpisodeCandidatesProvider(
    IEpisodeRepository episodeRepository,
    IPodcastRepositoryV2 podcastRepository,
    ILogger<RecentEpisodeCandidatesProvider> logger)
    : IRecentEpisodeCandidatesProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private DateTime _cachedAtUtc;
    private IReadOnlyCollection<Episode>? _cachedEpisodes;

    private DateTime? _cachedReleasedSince;

    public async Task<IReadOnlyCollection<Episode>> GetRecentActiveEpisodes(DateTime releasedSince)
    {
        var utcNow = DateTime.UtcNow;
        if (_cachedEpisodes != null &&
            _cachedReleasedSince == releasedSince &&
            utcNow - _cachedAtUtc <= CacheDuration)
        {
            logger.LogInformation(
                "Using cached recent episode candidates. Released-since: '{ReleasedSince:O}', Count: {Count}.",
                releasedSince,
                _cachedEpisodes.Count);
            return _cachedEpisodes;
        }

        await CacheLock.WaitAsync();
        try
        {
            utcNow = DateTime.UtcNow;
            if (_cachedEpisodes != null &&
                _cachedReleasedSince == releasedSince &&
                utcNow - _cachedAtUtc <= CacheDuration)
            {
                logger.LogInformation(
                    "Using cached recent episode candidates after lock. Released-since: '{ReleasedSince:O}', Count: {Count}.",
                    releasedSince,
                    _cachedEpisodes.Count);
                return _cachedEpisodes;
            }

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
                _cachedReleasedSince = releasedSince;
                _cachedAtUtc = utcNow;
                _cachedEpisodes = [];

                logger.LogInformation(
                    "No recently active podcasts found for candidate retrieval. Released-since: '{ReleasedSince:O}'.",
                    releasedSince);

                return _cachedEpisodes;
            }

            var episodes = new List<Episode>();
            foreach (var podcastId in recentPodcastIds)
            {
                var podcastEpisodes = await episodeRepository
                    .GetByPodcastId(podcastId, x => x.Release >= releasedSince && !x.Ignored && !x.Removed)
                    .ToArrayAsync();

                episodes.AddRange(podcastEpisodes);
            }

            _cachedReleasedSince = releasedSince;
            _cachedAtUtc = utcNow;
            _cachedEpisodes = episodes
                .OrderByDescending(x => x.Release)
                .ToArray();

            logger.LogInformation(
                "Loaded recent episode candidates via latestReleased-scoped partition reads. Released-since: '{ReleasedSince:O}', PodcastCount: {PodcastCount}, Count: {Count}.",
                releasedSince,
                recentPodcastIds.Length,
                _cachedEpisodes.Count);

            return _cachedEpisodes;
        }
        finally
        {
            CacheLock.Release();
        }
    }
}