using Api.Models;

namespace Api.Services.Episodes;

public interface IEpisodeOutgoingService
{
    Task<EpisodeOutgoingResult> GetOutgoingAsync(
        OutgoingEpisodesQuery query,
        CancellationToken cancellationToken);
}
