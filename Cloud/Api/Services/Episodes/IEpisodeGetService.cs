using Api.Models;

namespace Api.Services.Episodes;

public interface IEpisodeGetService
{
    Task<EpisodeGetResult> GetAsync(
        PodcastEpisodeRequestWrapper request,
        CancellationToken cancellationToken);
}
