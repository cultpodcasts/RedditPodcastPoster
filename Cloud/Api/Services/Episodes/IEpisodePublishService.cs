using Api.Dtos;
using Api.Models;

namespace Api.Services.Episodes;

public interface IEpisodePublishService
{
    Task<EpisodePublishResult> PublishAsync(
        EpisodePublishRequestWrapper publishRequest,
        CancellationToken cancellationToken);
}
