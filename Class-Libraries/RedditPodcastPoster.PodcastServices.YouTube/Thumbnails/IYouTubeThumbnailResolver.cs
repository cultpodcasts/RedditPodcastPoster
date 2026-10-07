namespace RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

public interface IYouTubeThumbnailResolver
{
    Task<Uri?> GetImageUrlAsync(
        Google.Apis.YouTube.v3.Data.Video? video,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Largest-first thumbnail URLs tall enough to use, without downloading them.
    /// A non-default image at the placeholder height is omitted. The default tier is kept.
    /// </summary>
    IReadOnlyList<Uri> GetUsableCandidateUrls(Google.Apis.YouTube.v3.Data.Video? video);
}
