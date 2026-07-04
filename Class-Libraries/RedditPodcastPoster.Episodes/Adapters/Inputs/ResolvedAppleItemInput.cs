namespace RedditPodcastPoster.Episodes.Adapters.Inputs;

public sealed record ResolvedAppleItemInput(
    long? EpisodeId,
    string EpisodeTitle,
    string EpisodeDescription,
    DateTime Release,
    TimeSpan Duration,
    Uri? Url,
    Uri? Image);
