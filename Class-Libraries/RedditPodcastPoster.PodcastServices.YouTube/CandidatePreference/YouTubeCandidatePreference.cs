namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

public class YouTubeCandidatePreference(
    IYouTubeThumbnailMotionDetector motionDetector,
    Microsoft.Extensions.Options.IOptions<YouTubeCandidatePreferenceOptions> options,
    Microsoft.Extensions.Logging.ILogger<YouTubeCandidatePreference> logger) : IYouTubeCandidatePreference
{
    public Task<string> ChooseAsync(
        YouTubeCandidate chosen,
        IReadOnlyList<YouTubeCandidate> contenders,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}