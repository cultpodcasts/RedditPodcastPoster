namespace Api.Models;

public record EpisodeTrailerAttachCommand(Guid PodcastId, Guid EpisodeId, EpisodeTrailerAttachRequest Request);
