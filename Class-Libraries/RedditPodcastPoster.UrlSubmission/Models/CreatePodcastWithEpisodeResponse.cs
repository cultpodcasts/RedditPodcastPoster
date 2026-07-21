using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.UrlSubmission.Models;

public record CreatePodcastWithEpisodeResponse(
    Podcast NewPodcast,
    Episode NewEpisode,
    SubmitEpisodeDetails SubmitEpisodeDetails);