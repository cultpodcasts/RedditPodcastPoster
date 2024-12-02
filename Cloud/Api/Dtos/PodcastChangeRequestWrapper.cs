namespace Api.Dtos;

public record PodcastChangeRequestWrapper(Guid PodcastId, Podcast Podcast, bool AllowNameChange = false);