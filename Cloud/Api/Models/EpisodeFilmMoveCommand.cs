namespace Api.Models;

public record EpisodeFilmMoveCommand(Guid PodcastId, Guid EpisodeId, EpisodeFilmMoveRequest Request);
