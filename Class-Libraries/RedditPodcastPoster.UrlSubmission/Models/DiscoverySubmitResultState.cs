namespace RedditPodcastPoster.UrlSubmission.Models;

public enum DiscoverySubmitResultState
{
    NoUrls = 1,
    DifferentPodcasts,
    CreatedPodcastAndEpisode,
    CreatedEpisode,
    EnrichedPodcastAndEpisode,
    EnrichedPodcastAndCreatedEpisode,
    EpisodeAlreadyExists,
    EnrichedPodcast,
    EnrichedEpisode
}