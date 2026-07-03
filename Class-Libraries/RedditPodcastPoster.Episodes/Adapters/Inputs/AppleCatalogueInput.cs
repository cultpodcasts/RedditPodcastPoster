namespace RedditPodcastPoster.Episodes.Adapters.Inputs;

public sealed record AppleCatalogueInput(
    long AppleId,
    string Title,
    string Description,
    TimeSpan Duration,
    DateTime Release,
    Uri AppleUrl,
    Uri? Image);
