using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.TvShows;

public class TvShowEpisodeGetService(
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    ILogger<TvShowEpisodeGetService> logger) : ITvShowEpisodeGetService
{
    public async Task<TvShowEpisodeGetResult> GetAsync(Guid episodeId, CancellationToken cancellationToken)
    {
        try
        {
            var episode = await tvShowEpisodeRepository.GetBy(item => item.Id == episodeId);
            return episode is null
                ? new TvShowEpisodeGetResult(TvShowEpisodeGetStatus.NotFound)
                : new TvShowEpisodeGetResult(TvShowEpisodeGetStatus.Found, episode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get TV-show episode '{id}'.", nameof(GetAsync), episodeId);
            return new TvShowEpisodeGetResult(TvShowEpisodeGetStatus.Failed);
        }
    }
}
