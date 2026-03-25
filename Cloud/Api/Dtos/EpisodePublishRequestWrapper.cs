namespace Api.Dtos;

public record EpisodePublishRequestWrapper(
    Guid? PodcastId,
    Guid EpisodeId,
    EpisodePublishRequest EpisodePublishRequest);