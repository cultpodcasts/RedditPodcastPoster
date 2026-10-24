using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Podcasts;

namespace Api.Services.Catalogue;

/// <summary>
/// Applies the curator's service ids to a news organisation's or TV show's <c>indexing</c> block, with the podcast
/// edit API rules from <c>PodcastChangeApplier</c>:
/// <list type="bullet">
/// <item><c>spotifyId</c>: trimmed; <c>""</c> clears (stored as null).</item>
/// <item><c>appleId</c> / <c>nullAppleId</c>: <c>nullAppleId: true</c> clears, otherwise <c>appleId</c> sets.</item>
/// <item><c>youTubePlaylistId</c>: the <c>YouTubePlaylistIdChange</c> rule. Whitespace clears, the value is
/// trimmed, a replaced non-empty id is appended to <c>youTubePlaylistIdHistory</c>, and a Warning is logged.</item>
/// <item><c>youTubeChannelId</c>: trimmed; <c>""</c> clears (stored as null). The podcast DTO accepts it but its applier never
/// stores it. Here it is stored, because it is what URL submission matches a YouTube channel on.</item>
/// </list>
/// A cleared value is stored as null (omitted), never <c>""</c>; <see cref="CatalogueIndexingSource"/> normalises blanks,
/// so a new block holds only the ids actually set, not empty regexes and ids.
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
            source.YouTubeChannelId = NullIfBlank(change.YouTubeChannelId);
            setsSomething |= source.YouTubeChannelId != null;
        }

        if (change.SpotifyId != null)
        {
            source.SpotifyId = NullIfBlank(change.SpotifyId);
            setsSomething |= source.SpotifyId != null;
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

                source.YouTubePlaylistId = measured.Length > 0 ? measured : null;
                logger.LogWarning(
                    "YouTube playlist id changed: parent-id='{ParentId}' parent-name='{ParentName}' previous='{Previous}' measured='{Measured}'",
                    parent.Id, parent.Name, previous, measured);
            }

            setsSomething |= measured.Length > 0;
        }

        if (change.IndexAllEpisodes != null)
        {
            source.IndexAllEpisodes = change.IndexAllEpisodes.Value;
            // Same mutual exclusion as PodcastChangeApplier: Index All skips the include filter.
            if (change.IndexAllEpisodes.Value)
            {
                source.EpisodeIncludeTitleRegex = null;
            }

            setsSomething = true;
        }

        if (change.TitleRegex != null)
        {
            source.TitleRegex = NullIfBlank(change.TitleRegex);
            setsSomething |= source.TitleRegex != null;
        }

        if (change.DescriptionRegex != null)
        {
            source.DescriptionRegex = NullIfBlank(change.DescriptionRegex);
            setsSomething |= source.DescriptionRegex != null;
        }

        if (change.EpisodeMatchRegex != null)
        {
            source.EpisodeMatchRegex = NullIfBlank(change.EpisodeMatchRegex);
            setsSomething |= source.EpisodeMatchRegex != null;
        }

        if (change.EpisodeIncludeTitleRegex != null)
        {
            source.EpisodeIncludeTitleRegex = NullIfBlank(change.EpisodeIncludeTitleRegex);
            if (!string.IsNullOrWhiteSpace(change.EpisodeIncludeTitleRegex))
            {
                source.IndexAllEpisodes = false;
            }

            setsSomething |= source.EpisodeIncludeTitleRegex != null || change.EpisodeIncludeTitleRegex.Trim().Length == 0;
        }

        return setsSomething ? source : indexing;
    }

    /// <summary>Trimmed value, or null when blank: indexing text members are never stored as <c>""</c>.</summary>
    private static string? NullIfBlank(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0 ? trimmed : null;
    }
}
