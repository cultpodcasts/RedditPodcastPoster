namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

public class YouTubeThumbnailMotionDetector : IYouTubeThumbnailMotionDetector
{
    public const string HttpClientName = nameof(YouTubeThumbnailMotionDetector);

    public Task<ThumbnailMotion> DetectAsync(string videoId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}