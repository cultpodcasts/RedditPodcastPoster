namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>
/// Detects static-image uploads by comparing the three small auto thumbnails
/// (i.ytimg.com/vi/&lt;id&gt;/1.jpg, 2.jpg, 3.jpg). Never downloads video.
/// </summary>
public interface IYouTubeThumbnailMotionDetector
{
    Task<ThumbnailMotion> DetectAsync(string videoId, CancellationToken cancellationToken = default);
}