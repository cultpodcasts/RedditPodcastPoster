namespace Api.Models;

public record EpisodePublishRequestWrapper(
    Guid? PodcastId,
    Guid EpisodeId,
    EpisodePublishRequest EpisodePublishRequest);