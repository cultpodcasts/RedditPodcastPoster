using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

/// <summary>
/// Applies a new <see cref="Podcast.YouTubePlaylistId"/> and logs when the value actually changes
/// so operators can see curated show playlist swaps (e.g. unlisted → public show playlist) in App Insights.
/// </summary>
public static class YouTubePlaylistIdChange
{
    public const string ChangedMessagePrefix = "YouTube playlist id changed:";

    public const string ChangedMessageTemplate =
        ChangedMessagePrefix +
        " podcast-id='{PodcastId}' podcast-name='{PodcastName}' previous='{Previous}' measured='{Measured}'";

    /// <summary>
    /// Sets <paramref name="newPlaylistId"/> on the podcast when it differs from the stored value.
    /// Null / whitespace is normalized to empty. Returns true when the stored id changed.
    /// </summary>
    public static bool Apply(Podcast podcast, string? newPlaylistId, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(podcast);

        var measured = string.IsNullOrWhiteSpace(newPlaylistId) ? string.Empty : newPlaylistId.Trim();
        var previous = podcast.YouTubePlaylistId ?? string.Empty;
        if (string.Equals(previous, measured, StringComparison.Ordinal))
        {
            return false;
        }

        podcast.YouTubePlaylistId = measured;
        logger?.LogWarning(
            ChangedMessageTemplate,
            podcast.Id,
            podcast.Name,
            previous,
            measured);
        return true;
    }
}
