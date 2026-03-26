using Api.Dtos;

namespace Api.Models;

public record EpisodeChangeRequestWrapper(
    Guid? PodcastId,
    Guid EpisodeId,
    EpisodeChangeRequest EpisodeChangeRequest);