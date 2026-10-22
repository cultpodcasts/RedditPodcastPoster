namespace Api.Models;

public record EpisodeTvShowMoveCommand(Guid PodcastId, Guid EpisodeId, EpisodeTvShowMoveRequest Request);
