namespace RedditPodcastPoster.Models;

/// <summary>
/// Pairs a detached `V2.Podcast` with a detached `V2.Episode`.
/// </summary>
public record PodcastEpisode(Podcast Podcast, Episode Episode);
