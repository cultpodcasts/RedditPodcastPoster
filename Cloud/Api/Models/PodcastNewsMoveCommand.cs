namespace Api.Models;

/// <summary>
/// POST podcast/{podcastId}/news: move the listed episodes of one podcast to one news organisation. Only
/// <see cref="EpisodeIds"/> move; the podcast's other episodes stay.
/// </summary>
public record PodcastNewsMoveCommand(
    Guid PodcastId,
    IReadOnlyList<Guid> EpisodeIds,
    EpisodeNewsMoveRequest Request);
