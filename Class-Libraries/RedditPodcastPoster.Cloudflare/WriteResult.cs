namespace RedditPodcastPoster.Cloudflare;

public record WriteResult(bool Success, Uri? Url = null);