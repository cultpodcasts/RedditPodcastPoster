namespace RedditPodcastPoster.Common.UrlCategorisation;

public record PodcastServiceSearchCriteria(
    string ShowName,
    string ShowDescription,
    string Publisher,
    string EpisodeTitle,
    string EpisodeDescription,
    DateTime Release,
    TimeSpan Duration);