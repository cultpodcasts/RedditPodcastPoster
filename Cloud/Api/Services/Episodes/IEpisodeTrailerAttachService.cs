using Api.Models;

namespace Api.Services.Episodes;

public interface IEpisodeTrailerAttachService
{
    Task<EpisodeTrailerAttachResult> AttachAsync(EpisodeTrailerAttachCommand command, CancellationToken cancellationToken);
}
