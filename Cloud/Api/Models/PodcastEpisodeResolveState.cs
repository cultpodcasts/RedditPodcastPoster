namespace Api.Models;

public enum PodcastEpisodeResolveState
{
    Unknown = 0,
    Resolved,
    PodcastConflict,
    PodcastNotFound,
}