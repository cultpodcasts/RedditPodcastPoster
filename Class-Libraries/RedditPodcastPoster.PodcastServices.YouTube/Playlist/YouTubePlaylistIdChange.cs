using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

/// <summary>
/// Applies a new <see cref="Podcast.YouTubePlaylistId"/>, appends the previous non-empty id to
/// <see cref="Podcast.YouTubePlaylistIdHistory"/>, and logs when the value actually changes so
/// operators can see curated show playlist swaps and recover if a swap was wrong.
/// </summary>
public static class YouTubePlaylistIdChange
{
    public const string ChangedMessagePrefix = "YouTube playlist id changed:";

    public const string ChangedMessageTemplate =
        ChangedMessagePrefix +
        " podcast-id='{PodcastId}' podcast-name='{PodcastName}' previous='{Previous}' measured='{Measured}'";

    /// <summary>
    /// Sets <paramref name="newPlaylistId"/> on the podcast when it differs from the stored value.
    /// Null / whitespace is normalized to empty. When the previous id was non-empty it is appended
    /// to history with <paramref name="replacedAtUtc"/> (defaults to <see cref="DateTime.UtcNow"/>).
    /// Returns true when the stored id changed.
    /// </summary>
    public static bool Apply(
        Podcast podcast,
        string? newPlaylistId,
        ILogger? logger = null,
        DateTime? replacedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(podcast);

        var measured = string.IsNullOrWhiteSpace(newPlaylistId) ? string.Empty : newPlaylistId.Trim();
        var previous = podcast.YouTubePlaylistId ?? string.Empty;
        if (string.Equals(previous, measured, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(previous))
        {
            podcast.YouTubePlaylistIdHistory ??= [];
            podcast.YouTubePlaylistIdHistory.Add(new YouTubePlaylistIdHistoryEntry
            {
                Id = previous,
                ReplacedAt = replacedAtUtc ?? DateTime.UtcNow
            });
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
