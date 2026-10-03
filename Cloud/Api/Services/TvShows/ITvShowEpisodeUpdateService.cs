using Api.Models;

namespace Api.Services.TvShows;

public interface ITvShowEpisodeUpdateService
{
    Task<TvShowEpisodeUpdateResult> UpdateAsync(
        TvShowEpisodeChangeRequestWrapper request,
        CancellationToken cancellationToken);
}
