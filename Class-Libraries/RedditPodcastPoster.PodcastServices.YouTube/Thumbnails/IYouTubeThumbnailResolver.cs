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

    /// <summary>
    /// Downloads a stored thumbnail URL and reports whether it is still a real image:
    /// a success response whose JPEG is taller than the placeholder (the default tier only needs dimensions).
    /// </summary>
    Task<bool> IsStoredImageUsableAsync(Uri imageUrl, CancellationToken cancellationToken = default);
}
