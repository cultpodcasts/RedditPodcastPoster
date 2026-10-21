namespace RedditPodcastPoster.Bluesky.Client;

public interface IBlueskyCardImageDownloader
{
    Task<BlueskyCardImage?> Download(IReadOnlyList<Uri> imageUrls, CancellationToken cancellationToken = default);
}
