using Api.Models;

namespace Api.Services.Episodes;

public interface IEpisodeTvShowTrailerAttachService
{
    Task<EpisodeTvShowTrailerAttachResult> AttachAsync(
        EpisodeTvShowTrailerAttachCommand command,
        CancellationToken cancellationToken);
}

