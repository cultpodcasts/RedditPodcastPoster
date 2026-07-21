namespace RedditPodcastPoster.Cloudflare.Models;

public record WriteResult(bool Success, Uri? Url = null);
