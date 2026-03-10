using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.UrlSubmission.Models;

public record CreatePodcastWithEpisodeResponse(
    Podcast NewPodcast,
    Episode NewEpisode,
    SubmitEpisodeDetails SubmitEpisodeDetails);