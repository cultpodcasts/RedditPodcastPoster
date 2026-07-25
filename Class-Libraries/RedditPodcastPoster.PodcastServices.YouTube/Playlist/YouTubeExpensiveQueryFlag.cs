using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

/// <summary>
/// Applies a measured YouTube playlist-order probe to <see cref="Podcast.YouTubePlaylistQueryIsExpensive"/>.
/// Playlists can flip between newest-first and oldest-first, so a conclusive probe both sets and clears
/// the flag — sticky-true alone permanently misclassifies flipped playlists.
/// </summary>
public static class YouTubeExpensiveQueryFlag
{
    /// <summary>
    /// Minimum playlist items required before treating reverse-chrono vs ascending as conclusive.
    /// A single item cannot distinguish playlist order.
    /// </summary>
    public const int MinimumOrderSampleSize = 2;

    public const string FlagFlippedMessagePrefix = "YouTube expensive-query flag flipped:";

    public const string FlagFlippedMessageTemplate =
        "YouTube expensive-query flag flipped: podcast-id='{PodcastId}' podcast-name='{PodcastName}' youtube-playlist-id='{YouTubePlaylistId}' previous='{Previous}' measured='{Measured}'";

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

        var previous = podcast.YouTubePlaylistQueryIsExpensive;
        var measured = measuredExpensive.Value;
        if (previous == measured)
        {
            return false;
        }

        podcast.YouTubePlaylistQueryIsExpensive = measured;
        logger?.LogWarning(
            FlagFlippedMessageTemplate,
            podcast.Id,
            podcast.Name,
            podcast.YouTubePlaylistId,
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
