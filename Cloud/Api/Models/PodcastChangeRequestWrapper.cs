using Api.Dtos;

namespace Api.Models;

public record PodcastChangeRequestWrapper(Guid PodcastId, Podcast Podcast, bool AllowNameChange = false);
