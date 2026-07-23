namespace Api.Models;

public record PodcastChangeRequestWrapper(Guid PodcastId, PodcastChangeRequest Podcast, bool AllowNameChange = false);