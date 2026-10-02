using Api.Models;

namespace Api.Services.TvShows;

public interface ITvShowUpdateService
{
    Task<TvShowUpdateResult> UpdateAsync(
        TvShowChangeRequestWrapper request,
        CancellationToken cancellationToken);
}
