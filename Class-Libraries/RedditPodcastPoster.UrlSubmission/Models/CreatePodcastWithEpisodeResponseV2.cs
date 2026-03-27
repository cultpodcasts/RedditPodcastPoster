using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.UrlSubmission.Models;

/// <summary>
/// V2 response containing newly created podcast and episode using V2 models.
/// </summary>
public record CreatePodcastWithEpisodeResponseV2(
    Podcast NewPodcast,
    Episode NewEpisode,
    SubmitEpisodeDetails SubmitEpisodeDetails);
