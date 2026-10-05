using RedditPodcastPoster.TheTvdb.Models;

namespace RedditPodcastPoster.TheTvdb.Clients;

public interface ITheTvdbClient
{
    Task<IReadOnlyList<TheTvdbSearchHit>> SearchAsync(
        string query,
        TheTvdbSearchType type,
        CancellationToken cancellationToken = default);

    Task<TheTvdbSeries?> GetSeriesAsync(long seriesId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TheTvdbEpisode>> GetEpisodesAsync(long seriesId, CancellationToken cancellationToken = default);

    Task<TheTvdbEpisode?> GetEpisodeAsync(long episodeId, CancellationToken cancellationToken = default);
}
