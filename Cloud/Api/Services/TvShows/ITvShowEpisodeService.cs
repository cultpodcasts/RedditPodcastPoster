using Api.Models;

namespace Api.Services.TvShows;

public interface ITvShowEpisodeService
{
    Task<TvShowEpisodeGetResult> GetAsync(Guid episodeId, CancellationToken cancellationToken);

    Task<TvShowEpisodeUpdateResult> UpdateAsync(
        TvShowEpisodeChangeRequestWrapper request,
        CancellationToken cancellationToken);

    Task<EpisodeDeleteResult> DeleteAsync(Guid episodeId, CancellationToken cancellationToken);
}
