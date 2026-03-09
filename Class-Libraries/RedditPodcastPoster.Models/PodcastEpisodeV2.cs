namespace RedditPodcastPoster.Models;

/// <summary>
/// V2 version of PodcastEpisode that uses detached V2 models instead of legacy embedded models.
/// Pairs a V2.Podcast with a V2.Episode from the detached episode architecture.
/// </summary>
public record PodcastEpisodeV2(Models.V2.Podcast Podcast, Models.V2.Episode Episode);
