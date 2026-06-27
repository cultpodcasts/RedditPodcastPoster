namespace RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

public interface IYouTubeThumbnailResolver
{
    Task<Uri?> GetImageUrlAsync(
        Google.Apis.YouTube.v3.Data.Video? video,
        CancellationToken cancellationToken = default);
}
