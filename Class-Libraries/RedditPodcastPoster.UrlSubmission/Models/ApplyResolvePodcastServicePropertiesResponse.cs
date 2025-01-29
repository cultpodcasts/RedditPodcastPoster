namespace RedditPodcastPoster.UrlSubmission.Models;

public record ApplyResolvePodcastServicePropertiesResponse(
    SubmitResultState PodcastResult,
    SubmitResultState AppliedEpisodeResult,
    SubmitEpisodeDetails SubmitEpisodeDetails);