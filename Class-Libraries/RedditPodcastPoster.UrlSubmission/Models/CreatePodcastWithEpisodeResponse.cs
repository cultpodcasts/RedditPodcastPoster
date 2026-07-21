using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.UrlSubmission.Models;

public record CreatePodcastWithEpisodeResponse(
    Podcast NewPodcast,
    Episode NewEpisode,
    SubmitEpisodeDetails SubmitEpisodeDetails);
