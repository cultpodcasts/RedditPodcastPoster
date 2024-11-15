namespace RedditPodcastPoster.UrlSubmission;

public record SubmitEpisodeDetails(
    bool Spotify,
    bool Apple,
    bool YouTube,
    string[]? Subjects = null);