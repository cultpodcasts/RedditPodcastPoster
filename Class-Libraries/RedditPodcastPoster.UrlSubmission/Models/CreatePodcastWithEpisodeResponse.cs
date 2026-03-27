using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.UrlSubmission.Models;

public record CreatePodcastWithEpisodeResponse(
    Podcast NewPodcast,
    Episode NewEpisode,
    SubmitEpisodeDetails SubmitEpisodeDetails);