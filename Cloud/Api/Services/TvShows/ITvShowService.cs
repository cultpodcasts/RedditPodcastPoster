using Api.Models;

namespace Api.Services.TvShows;

public interface ITvShowService
{
    Task<TvShowGetResult> GetAsync(string identifier, CancellationToken cancellationToken);

    Task<TvShowUpdateResult> UpdateAsync(
        TvShowChangeRequestWrapper request,
        CancellationToken cancellationToken);
}
