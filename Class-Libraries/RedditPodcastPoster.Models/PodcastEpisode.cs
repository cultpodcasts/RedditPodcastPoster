namespace RedditPodcastPoster.Models;

/// <summary>
/// Pairs a detached `Podcast` with a detached `Episode`.
/// </summary>
public record PodcastEpisode(Podcast Podcast, Episode Episode);
