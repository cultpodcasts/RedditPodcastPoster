namespace RedditPodcastPoster.Episodes.Adapters.Inputs;

public sealed record SpotifyCatalogueInput(
    string SpotifyId,
    string Title,
    string Description,
    TimeSpan Duration,
    DateTime Release,
    Uri SpotifyUrl,
    Uri? Image);
