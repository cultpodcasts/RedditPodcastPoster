using Api.Models;

namespace Api.Services.Public;

public interface IPublicEpisodeGetService
{
    Task<PublicEpisodeGetResult> GetAsync(
        PodcastEpisodeRequestWrapper request,
        CancellationToken cancellationToken);
}
