using Api.Models;

namespace Api.Services.Episodes;

public interface IEpisodeUpdateService
{
    Task<EpisodeUpdateResult> UpdateAsync(
        EpisodeChangeRequestWrapper request,
        CancellationToken cancellationToken);
}
