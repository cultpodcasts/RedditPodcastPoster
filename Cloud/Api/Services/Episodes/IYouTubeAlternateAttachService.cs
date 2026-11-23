using Api.Models;

namespace Api.Services.Episodes;

public interface IYouTubeAlternateAttachService
{
    Task<EpisodeYouTubeAlternateAttachResult> AttachAsync(
        EpisodeYouTubeAlternateAttachCommand command,
        CancellationToken cancellationToken);
}
