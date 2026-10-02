using Api.Models;

namespace Api.Services.TvShows;

public interface ITvShowGetService
{
    Task<TvShowGetResult> GetAsync(string identifier, CancellationToken cancellationToken);
}
