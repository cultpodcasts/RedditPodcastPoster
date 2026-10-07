namespace RedditPodcastPoster.Bluesky.Client;

public interface IBlueskyCardImageDownloader
{
    Task<BlueskyCardImage?> Download(Uri imageUrl, CancellationToken cancellationToken = default);
}
