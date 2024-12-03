namespace RedditPodcastPoster.UrlSubmission;

public record ApplyResolvePodcastServicePropertiesResponse(
    SubmitResult.SubmitResultState PodcastResult,
    SubmitResult.SubmitResultState AppliedEpisodeResult,
    SubmitEpisodeDetails SubmitEpisodeDetails);