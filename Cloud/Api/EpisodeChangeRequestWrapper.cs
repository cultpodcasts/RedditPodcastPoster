using Api.Dtos;

namespace Api;

public record EpisodeChangeRequestWrapper(Guid EpisodeId, EpisodeChangeRequest EpisodeChangeRequest);