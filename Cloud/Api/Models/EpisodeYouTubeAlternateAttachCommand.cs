namespace Api.Models;

public sealed record EpisodeYouTubeAlternateAttachCommand(
    Guid PodcastId,
    Guid EpisodeId,
    EpisodeYouTubeAlternateAttachRequest Request);
