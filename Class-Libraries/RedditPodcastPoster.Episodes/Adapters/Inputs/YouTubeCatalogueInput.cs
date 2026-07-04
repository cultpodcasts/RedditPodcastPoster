namespace RedditPodcastPoster.Episodes.Adapters.Inputs;

public sealed record YouTubeCatalogueInput(
    string YouTubeId,
    string Title,
    string Description,
    TimeSpan Duration,
    DateTime Release,
    Uri YouTubeUrl,
    Uri? Image);
