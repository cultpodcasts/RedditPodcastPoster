using Api.Models;

namespace Api.Services.Episodes;

public interface IEpisodeDeleteService
{
    Task<EpisodeDeleteResult> DeleteAsync(
        PodcastEpisodeRequestWrapper request,
        CancellationToken cancellationToken);
}
