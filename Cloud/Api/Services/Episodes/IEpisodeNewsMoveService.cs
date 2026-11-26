using Api.Models;

namespace Api.Services.Episodes;

public interface IEpisodeNewsMoveService
{
    /// <summary>Moves one podcast episode to a news organisation (POST episode/{podcastId}/{episodeId}/news).</summary>
    Task<EpisodeNewsMoveResult> MoveAsync(EpisodeNewsMoveCommand command, CancellationToken cancellationToken);

    /// <summary>Moves the listed episodes of one podcast to one news organisation (POST podcast/{podcastId}/news).</summary>
    Task<EpisodeNewsMoveResult> MoveManyAsync(PodcastNewsMoveCommand command, CancellationToken cancellationToken);
}
