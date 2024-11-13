using Api.Dtos;

namespace Api.Models;

public record EpisodeChangeRequestWrapper(
    Guid EpisodeId,
    EpisodeChangeRequest EpisodeChangeRequest);