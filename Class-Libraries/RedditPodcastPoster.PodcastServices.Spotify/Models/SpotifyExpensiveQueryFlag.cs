using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Spotify.Models;

/// <summary>
/// Applies a measured Spotify catalogue-order probe to <see cref="Podcast.SpotifyEpisodesQueryIsExpensive"/>.
/// Spotify can flip a show between newest-first and oldest-first, so a conclusive probe both sets and clears
/// the flag — sticky-true alone permanently misclassifies flipped shows.
/// </summary>
public static class SpotifyExpensiveQueryFlag
{
    /// <summary>
    /// Minimum distinct lead-in episodes required before treating reverse-chrono vs ascending as conclusive.
    /// A single episode cannot distinguish catalogue order.
    /// </summary>
    public const int MinimumOrderSampleSize = 2;

    public const string FlagFlippedMessagePrefix = "Spotify expensive-query flag flipped:";

    public const string FlagFlippedMessageTemplate =
        "Spotify expensive-query flag flipped: podcast-id='{PodcastId}' podcast-name='{PodcastName}' spotify-id='{SpotifyId}' previous='{Previous}' measured='{Measured}'";

    /// <summary>
    /// Writes <paramref name="measuredExpensive"/> onto the podcast when the probe is conclusive.
    /// Returns true when the stored flag value changed.
    /// </summary>
    public static bool Apply(
        Podcast podcast,
        bool? measuredExpensive,
        int orderSampleSize,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(podcast);

        if (!measuredExpensive.HasValue || orderSampleSize < MinimumOrderSampleSize)
        {
            return false;
        }

        var previous = podcast.SpotifyEpisodesQueryIsExpensive;
        var measured = measuredExpensive.Value;
        if (previous == measured)
        {
            return false;
        }

        podcast.SpotifyEpisodesQueryIsExpensive = measured;
        logger?.LogWarning(
            FlagFlippedMessageTemplate,
            podcast.Id,
            podcast.Name,
            podcast.SpotifyId,
            previous,
            measured);
        return true;
    }

    /// <summary>
    /// Convenience overload when the caller only has a conclusive bool (sample already validated).
    /// </summary>
    public static bool Apply(
        Podcast podcast,
        bool measuredExpensive,
        ILogger? logger = null) =>
        Apply(podcast, measuredExpensive, MinimumOrderSampleSize, logger);
}
