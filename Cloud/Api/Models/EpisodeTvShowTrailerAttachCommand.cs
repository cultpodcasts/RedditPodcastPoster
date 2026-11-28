namespace Api.Models;

public record EpisodeTvShowTrailerAttachCommand(Guid PodcastId, Guid EpisodeId, EpisodeTvShowTrailerAttachRequest Request);

