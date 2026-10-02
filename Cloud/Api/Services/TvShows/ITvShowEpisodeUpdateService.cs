using Api.Models;

namespace Api.Services.TvShows;

public interface ITvShowEpisodeUpdateService
{
    Task<TvShowUpdateResult> UpdateAsync(
        TvShowEpisodeChangeRequestWrapper request,
        CancellationToken cancellationToken);
}
