namespace RedditPodcastPoster.UrlShortening;

public record WriteResult(bool Success, Uri? Url = null);