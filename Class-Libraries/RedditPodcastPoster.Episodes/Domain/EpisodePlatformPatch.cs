namespace RedditPodcastPoster.Episodes.Domain;

public sealed record EpisodePlatformPatch(
    PlatformLink? Link,
    string? Description,
    ReleaseInfo? Release);
