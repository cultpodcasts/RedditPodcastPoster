using Api.Models;

namespace Api.Services.Episodes;

public interface IEpisodeOutgoingService
{
    Task<EpisodeOutgoingResult> GetOutgoingAsync(
        OutgoingEpisodesQuery query,
        CancellationToken cancellationToken);

    Task<EpisodeOutgoingResult> GetOutgoingAsync(
        string? days,
        string? posted,
        string? tweeted,
        string? blueskyPosted,
        CancellationToken cancellationToken);
}
