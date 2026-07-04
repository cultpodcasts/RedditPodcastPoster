namespace RedditPodcastPoster.Episodes.Domain;

public sealed record ReleaseInfo(
    DateTime Value,
    ReleasePrecision Precision);
