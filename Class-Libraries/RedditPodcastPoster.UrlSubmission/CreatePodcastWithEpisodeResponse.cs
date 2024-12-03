using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.UrlSubmission;

public record CreatePodcastWithEpisodeResponse(
    Podcast NewPodcast,
    Episode NewEpisode,
    SubmitEpisodeDetails SubmitEpisodeDetails);