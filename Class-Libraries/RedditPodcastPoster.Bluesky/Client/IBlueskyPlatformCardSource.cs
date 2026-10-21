namespace RedditPodcastPoster.Bluesky.Client;

public interface IBlueskyPlatformCardSource
{
    Task<BlueskyPlatformCard?> TryGetAsync(Uri url, CancellationToken cancellationToken = default);
}
