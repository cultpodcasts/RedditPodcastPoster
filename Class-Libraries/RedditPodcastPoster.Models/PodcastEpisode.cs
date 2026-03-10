namespace RedditPodcastPoster.Models;

/// <summary>
/// Pairs a detached `V2.Podcast` with a detached `V2.Episode`.
/// </summary>
public record PodcastEpisode(Models.V2.Podcast Podcast, Models.V2.Episode Episode);
