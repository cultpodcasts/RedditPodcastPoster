namespace Api.Models;

public record TvShowEpisodeChangeRequestWrapper(Guid EpisodeId, TvShowChangeRequest Change);
