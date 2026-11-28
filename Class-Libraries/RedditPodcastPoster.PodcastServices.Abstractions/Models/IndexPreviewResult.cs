namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

/// <summary>
/// Read-only outcome of a dry-run index of one podcast: how many episodes an index would add to the catalogue.
/// Nothing is saved to produce it (cultpodcasts/RedditPodcastPoster#1433).
/// </summary>
/// <param name="NewEpisodeCount">
/// Episodes the index would add: found on YouTube, Spotify or Apple, not already stored, not catalogue-owned, not
/// dropped by the short-episode floor, and not removed by elimination terms.
/// </param>
/// <param name="IgnoredEpisodeCount">The subset of <paramref name="NewEpisodeCount"/> that would be saved as ignored.</param>
public record IndexPreviewResult(int NewEpisodeCount, int IgnoredEpisodeCount);
