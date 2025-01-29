namespace RedditPodcastPoster.UrlSubmission.Models;

public record SubmitEpisodeDetails(
    bool Spotify,
    bool Apple,
    bool YouTube,
    string[]? Subjects = null,
    bool BBC = false,
    bool InternetArchive = false
);