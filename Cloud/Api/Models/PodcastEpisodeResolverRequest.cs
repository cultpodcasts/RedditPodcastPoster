namespace Api.Models;

public record PodcastEpisodeResolverRequest(Guid EpisodeId, Guid? PodcastId = null, string? PodcastName = null);