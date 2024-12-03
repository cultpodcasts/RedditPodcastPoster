namespace RedditPodcastPoster.UrlSubmission;

public record ApplyResolvePodcastServicePropertiesResponse(
    SubmitResultState PodcastResult,
    SubmitResultState AppliedEpisodeResult,
    SubmitEpisodeDetails SubmitEpisodeDetails);