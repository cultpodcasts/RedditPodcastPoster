namespace Api.Models;

public record TvShowEpisodeChangeRequestWrapper(Guid EpisodeId, TvShowEpisodeChangeRequest Change);
