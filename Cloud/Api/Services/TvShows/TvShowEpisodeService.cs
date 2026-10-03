using Api.Models;
using Api.Services.Catalogue;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.TvShows;

public class TvShowEpisodeService(
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    ILogger<TvShowEpisodeService> logger) : ITvShowEpisodeService
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

    public async Task<TvShowEpisodeUpdateResult> UpdateAsync(
        TvShowEpisodeChangeRequestWrapper request,
        CancellationToken cancellationToken)
    {
        try
        {
            var episode = await tvShowEpisodeRepository.GetBy(item => item.Id == request.EpisodeId);
            if (episode is null)
            {
                return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.NotFound);
            }

            if (!CanonicalUriPatch.TryApply(request.Change.Imdb, uri => episode.Imdb = uri, out var imdbError))
            {
                return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.BadRequest, imdbError);
            }

            if (!CanonicalUriPatch.TryApply(request.Change.Tvdb, uri => episode.Tvdb = uri, out var tvdbError))
            {
                return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.BadRequest, tvdbError);
            }

            await tvShowEpisodeRepository.Save(episode);
            return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update TV-show episode '{id}'.", nameof(UpdateAsync),
                request.EpisodeId);
            return new TvShowEpisodeUpdateResult(TvShowEpisodeUpdateStatus.Failed);
        }
    }
}
