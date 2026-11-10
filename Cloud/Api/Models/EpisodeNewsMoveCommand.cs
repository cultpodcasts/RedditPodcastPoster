namespace Api.Models;

/// <summary>POST episode/{podcastId}/{episodeId}/news: move one podcast episode to a news organisation.</summary>
public record EpisodeNewsMoveCommand(Guid PodcastId, Guid EpisodeId, EpisodeNewsMoveRequest Request);
