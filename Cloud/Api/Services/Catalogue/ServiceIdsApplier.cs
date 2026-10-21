using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Podcasts;

namespace Api.Services.Catalogue;

/// <summary>
/// Applies the curator's service ids to a news organisation's or TV show's <c>indexing</c> block, with the podcast
/// edit API rules from <c>PodcastChangeApplier</c>:
/// <list type="bullet">
/// <item><c>spotifyId</c>: set; <c>""</c> clears.</item>
/// <item><c>appleId</c> / <c>nullAppleId</c>: <c>nullAppleId: true</c> clears, otherwise <c>appleId</c> sets.</item>
/// <item><c>youTubePlaylistId</c>: the <c>YouTubePlaylistIdChange</c> rule. Whitespace clears, the value is
/// trimmed, a replaced non-empty id is appended to <c>youTubePlaylistIdHistory</c>, and a Warning is logged.</item>
/// <item><c>youTubeChannelId</c>: trimmed; <c>""</c> clears. The podcast DTO accepts it but its applier never
/// stores it. Here it is stored, because it is what URL submission matches a YouTube channel on.</item>
/// </list>
/// The block is created only when a value is actually set. It is then not auto-indexed (<c>indexAllEpisodes</c>
/// false, no include regex), so the hourly indexer from #1050 leaves the parent alone until discovery is configured.
/// </summary>
public static class ServiceIdsApplier
{
    public static CatalogueIndexingSource? Apply(
        CatalogueIndexingSource? indexing,
        ServiceIdsChange change,
        Publisher parent,
        ILogger logger,
        DateTime? replacedAtUtc = null)
    {
        if (!change.ChangesServiceIds)
        {
            return indexing;
        }

        var source = indexing ?? new CatalogueIndexingSource();
        var setsSomething = indexing != null;

        if (change.YouTubeChannelId != null)
        {
            source.YouTubeChannelId = change.YouTubeChannelId.Trim();
            setsSomething |= source.YouTubeChannelId.Length > 0;
        }

        if (change.SpotifyId != null)
        {
            source.SpotifyId = change.SpotifyId.Trim();
            setsSomething |= source.SpotifyId.Length > 0;
        }

        if (change.NullAppleId == true)
        {
            source.AppleId = null;
        }
        else if (change.AppleId.HasValue)
        {
            source.AppleId = change.AppleId.Value;
            setsSomething = true;
        }

        if (change.YouTubePlaylistId != null)
        {
            var measured = string.IsNullOrWhiteSpace(change.YouTubePlaylistId) ? "" : change.YouTubePlaylistId.Trim();
            var previous = source.YouTubePlaylistId ?? "";
            if (!string.Equals(previous, measured, StringComparison.Ordinal))
            {
                if (previous.Length > 0)
                {
                    source.YouTubePlaylistIdHistory ??= [];
                    source.YouTubePlaylistIdHistory.Add(new YouTubePlaylistIdHistoryEntry
                        { Id = previous, ReplacedAt = replacedAtUtc ?? DateTime.UtcNow });
                }

                source.YouTubePlaylistId = measured;
                logger.LogWarning(
                    "YouTube playlist id changed: parent-id='{ParentId}' parent-name='{ParentName}' previous='{Previous}' measured='{Measured}'",
                    parent.Id, parent.Name, previous, measured);
            }

            setsSomething |= measured.Length > 0;
        }

        return setsSomething ? source : indexing;
    }
}
