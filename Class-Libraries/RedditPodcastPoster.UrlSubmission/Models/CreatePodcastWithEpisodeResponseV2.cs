namespace RedditPodcastPoster.UrlSubmission.Models;

/// <summary>
/// V2 response containing newly created podcast and episode using V2 models.
/// </summary>
public record CreatePodcastWithEpisodeResponseV2(
    RedditPodcastPoster.Models.V2.Podcast NewPodcast,
    RedditPodcastPoster.Models.V2.Episode NewEpisode,
    SubmitEpisodeDetails SubmitEpisodeDetails);
