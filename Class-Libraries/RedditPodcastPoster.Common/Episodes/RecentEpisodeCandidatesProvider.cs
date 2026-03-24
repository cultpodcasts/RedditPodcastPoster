using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using Episode = RedditPodcastPoster.Models.V2.Episode;

namespace RedditPodcastPoster.Common.Episodes;

public class RecentEpisodeCandidatesProvider(
    IEpisodeRepository episodeRepository,
    ILogger<RecentEpisodeCandidatesProvider> logger)
    : IRecentEpisodeCandidatesProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim CacheLock = new(1, 1);

    private DateTime? _cachedReleasedSince;
    private DateTime _cachedAtUtc;
    private IReadOnlyCollection<Episode>? _cachedEpisodes;

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

            var episodes = await episodeRepository
                .GetAllBy(x => x.Release >= releasedSince && !x.Ignored && !x.Removed)
                .ToArrayAsync();

            _cachedReleasedSince = releasedSince;
            _cachedAtUtc = utcNow;
            _cachedEpisodes = episodes;

            logger.LogInformation(
                "Loaded recent episode candidates from repository. Released-since: '{ReleasedSince:O}', Count: {Count}.",
                releasedSince,
                episodes.Length);

            return _cachedEpisodes;
        }
        finally
        {
            CacheLock.Release();
        }
    }
}
