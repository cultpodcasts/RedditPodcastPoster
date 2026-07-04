namespace RedditPodcastPoster.Episodes.Adapters.Inputs;

public sealed record ResolvedSpotifyItemInput(
    string EpisodeId,
    string EpisodeTitle,
    string EpisodeDescription,
    DateTime Release,
    TimeSpan Duration,
    Uri? Url,
    Uri? Image);
