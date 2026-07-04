namespace RedditPodcastPoster.Episodes.Domain;

public sealed record EpisodeCandidate(
    string Title,
    string Description,
    TimeSpan Duration,
    ReleaseInfo Release,
    PlatformLink? SourceLink);
