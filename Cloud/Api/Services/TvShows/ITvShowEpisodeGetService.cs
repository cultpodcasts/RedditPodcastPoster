using Api.Models;

namespace Api.Services.TvShows;

public interface ITvShowEpisodeGetService
{
    Task<TvShowEpisodeGetResult> GetAsync(Guid episodeId, CancellationToken cancellationToken);
}
