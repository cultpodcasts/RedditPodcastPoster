namespace RedditPodcastPoster.Common.UrlCategorisation;

public record ResolvedAppleItem(
    long? ShowId,
    long? EpisodeId,
    string ShowName,
    string ShowDescription,
    string Publisher,
    string EpisodeTitle,
    string EpisodeDescription,
    DateTime Release,
    TimeSpan Duration,
    Uri Url,
    bool Explicit);