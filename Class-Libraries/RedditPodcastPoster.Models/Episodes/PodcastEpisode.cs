using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Models.Episodes;

/// <summary>
/// Pairs a detached `Podcast` with a detached `Episode`.
/// </summary>
public record PodcastEpisode(Podcast Podcast, Episode Episode);
