using System.Text.RegularExpressions;
using Api.Models;
using Api.Services.YouTube;
using RedditPodcastPoster.Models.Catalogue;

namespace Api.Services.News;

/// <summary>
/// A news organisation PATCH whose additional YouTube channels cannot be stored. 400, or 503 when
/// <paramref name="IsRetryable"/> (a playlist lookup failed).
/// </summary>
public sealed record AdditionalYouTubeChannelsRefusal(string Code, string Message, bool IsRetryable = false);

/// <summary>
/// The news organisation PATCH rules for <c>indexing.additionalYouTubeChannels</c> (#1370): an outlet with more than
/// one YouTube channel (for example a main channel and an archive channel) indexes and owns each of them, as a whole
/// channel or through that channel's own optional playlist. The body sends <c>additionalYouTubeChannels</c>
/// (<c>[{ youTubeChannelId, youTubePlaylistId? }]</c>) or the older <c>additionalYouTubeChannelIds</c> (ids only, each a
/// whole channel), never both. A list replaces the stored list and <c>[]</c> clears it; ids and playlists are trimmed,
/// entries with neither are dropped and an exact repeat is dropped. Each channel id must look like a YouTube channel id
/// (<c>UC</c> + 22 characters), so a handle or URL pasted by mistake is refused instead of silently indexing nothing,
/// and none may be the organisation's main channel after the PATCH. A new or changed playlist must exist and belong to
/// its channel (<see cref="CheckPlaylists"/>), with the main-channel playlist error codes. The indexer's per-channel
/// flags are kept for a channel that stays (the expensive-query flag only while its playlist is unchanged).
/// </summary>
public static partial class AdditionalYouTubeChannels
{
    public const string InvalidChannelIdCode = "invalidAdditionalYouTubeChannelId";

    public const string MainChannelCode = "additionalYouTubeChannelIsMainChannel";

    /// <summary>True when the PATCH sends a list of additional channels (either form).</summary>
    public static bool ListRequested(NewsOrganisationChangeRequest change) =>
        change.AdditionalYouTubeChannels != null || change.AdditionalYouTubeChannelIds != null;

    /// <summary>The body field the PATCH used for the list, for messages.</summary>
    public static string FieldName(NewsOrganisationChangeRequest change) =>
        change.AdditionalYouTubeChannelIds != null && change.AdditionalYouTubeChannels == null
            ? "additionalYouTubeChannelIds"
            : "additionalYouTubeChannels";

    /// <summary>True when the PATCH touches the main or the additional channels, so the rules must be checked.</summary>
    public static bool ChangeAffectsChannels(NewsOrganisationChangeRequest change) =>
        ListRequested(change) || change.YouTubeChannelId != null;

    /// <summary>The additional channels the organisation will hold after the PATCH (channel and playlist ids only).</summary>
    public static IReadOnlyList<AdditionalYouTubeChannel> Projected(
        CatalogueIndexingSource? indexing,
        NewsOrganisationChangeRequest change)
    {
        if (Requested(change) is { } requested)
        {
            return new CatalogueIndexingSource
                {
                    AdditionalYouTubeChannels =
                    [
                        .. requested.Select(x =>
                            AdditionalYouTubeChannel.ForPlaylist(x.YouTubeChannelId ?? string.Empty, x.YouTubePlaylistId))
                    ]
                }
                .AdditionalYouTubeChannels ?? [];
        }

        return indexing?.AdditionalYouTubeChannels ?? [];
    }

    /// <summary>Null when the projected channels may be stored; otherwise the 400 refusal. Makes no YouTube call.</summary>
    public static AdditionalYouTubeChannelsRefusal? Check(
        CatalogueIndexingSource? indexing,
        NewsOrganisationChangeRequest change)
    {
        if (!ChangeAffectsChannels(change))
        {
            return null;
        }

        if (change.AdditionalYouTubeChannels != null && change.AdditionalYouTubeChannelIds != null)
        {
            return new AdditionalYouTubeChannelsRefusal(
                InvalidChannelIdCode,
                "Send additionalYouTubeChannels or the older additionalYouTubeChannelIds, not both.");
        }

        if (Requested(change) is { } requested)
        {
            var seen = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var entry in requested)
            {
                var channelId = Trimmed(entry.YouTubeChannelId);
                var playlistId = Trimmed(entry.YouTubePlaylistId);
                if (channelId == null)
                {
                    if (playlistId != null)
                    {
                        return new AdditionalYouTubeChannelsRefusal(
                            InvalidChannelIdCode,
                            $"An additional YouTube channel with youTubePlaylistId '{playlistId}' has no youTubeChannelId.");
                    }

                    continue;
                }

                if (!ChannelIdPattern().IsMatch(channelId))
                {
                    return new AdditionalYouTubeChannelsRefusal(
                        InvalidChannelIdCode,
                        $"'{channelId}' is not a YouTube channel id (UC followed by 22 characters).");
                }

                if (seen.TryGetValue(channelId, out var earlierPlaylistId))
                {
                    if (!string.Equals(earlierPlaylistId, playlistId, StringComparison.Ordinal))
                    {
                        return new AdditionalYouTubeChannelsRefusal(
                            InvalidChannelIdCode,
                            $"YouTube channel '{channelId}' is listed more than once with different playlists; list it once.");
                    }

                    continue;
                }

                seen[channelId] = playlistId;
            }
        }

        var additional = Projected(indexing, change);
        var main = change.YouTubeChannelId != null
            ? Trimmed(change.YouTubeChannelId)
            : indexing?.YouTubeChannelId;
        if (main != null && additional.Any(x => string.Equals(x.YouTubeChannelId, main, StringComparison.Ordinal)))
        {
            return new AdditionalYouTubeChannelsRefusal(
                MainChannelCode,
                $"YouTube channel '{main}' is the main youTubeChannelId; it cannot also be an additional channel.");
        }

        return null;
    }

    /// <summary>
    /// Null when every new or changed playlist on an additional channel may be stored: it must be a well-formed playlist
    /// id that exists and is owned by that channel (one playlists.list per new playlist; an unchanged playlist costs no
    /// quota). Otherwise the refusal with the main-channel playlist codes: <c>youTubePlaylistIdInvalid</c>,
    /// <c>youTubePlaylistNotFound</c>, <c>youTubePlaylistChannelMismatch</c> (400), or a retryable
    /// <c>youTubePlaylistLookupFailed</c> (503).
    /// </summary>
    public static async Task<AdditionalYouTubeChannelsRefusal?> CheckPlaylists(
        CatalogueIndexingSource? indexing,
        NewsOrganisationChangeRequest change,
        IYouTubePlaylistGuard youTubePlaylistGuard,
        CancellationToken cancellationToken)
    {
        if (!ListRequested(change))
        {
            return null;
        }

        foreach (var channel in Projected(indexing, change))
        {
            if (channel.YouTubePlaylistId is not { } playlistId ||
                string.Equals(indexing?.AdditionalYouTubeChannelFor(channel.YouTubeChannelId)?.YouTubePlaylistId,
                    playlistId, StringComparison.Ordinal))
            {
                continue;
            }

            if (await youTubePlaylistGuard.CheckChannelOwnsPlaylist(
                    channel.YouTubeChannelId, playlistId, cancellationToken) is { IsAllowed: false } refused)
            {
                return new AdditionalYouTubeChannelsRefusal(
                    refused.Code!,
                    $"Additional YouTube channel '{channel.YouTubeChannelId}': {refused.Message}",
                    refused.IsRetryable);
            }
        }

        return null;
    }

    /// <summary>
    /// Stores the requested list on <paramref name="indexing"/> (created only when a channel is actually set). A channel
    /// that was already held keeps its indexer-owned <c>youTubeChannelSearchForbidden</c>, and its
    /// <c>youTubePlaylistQueryIsExpensive</c> while its playlist is unchanged. Returns the block to keep; unchanged when
    /// the PATCH leaves the list alone.
    /// </summary>
    public static CatalogueIndexingSource? Apply(CatalogueIndexingSource? indexing, NewsOrganisationChangeRequest change)
    {
        if (!ListRequested(change))
        {
            return indexing;
        }

        var channels = Projected(null, change)
            .Select(channel =>
            {
                if (indexing?.AdditionalYouTubeChannelFor(channel.YouTubeChannelId) is { } stored)
                {
                    channel.YouTubeChannelSearchForbidden = stored.YouTubeChannelSearchForbidden;
                    if (string.Equals(stored.YouTubePlaylistId, channel.YouTubePlaylistId, StringComparison.Ordinal))
                    {
                        channel.YouTubePlaylistQueryIsExpensive = stored.YouTubePlaylistQueryIsExpensive;
                    }
                }

                return channel;
            })
            .ToList();

        if (indexing == null)
        {
            var created = new CatalogueIndexingSource { AdditionalYouTubeChannels = channels };
            return created.AdditionalYouTubeChannels == null ? null : created;
        }

        indexing.AdditionalYouTubeChannels = channels;
        return indexing;
    }

    private static IReadOnlyList<AdditionalYouTubeChannelChange>? Requested(NewsOrganisationChangeRequest change)
    {
        if (change.AdditionalYouTubeChannels is { } channels)
        {
            return channels.Where(x => x != null).ToList();
        }

        return change.AdditionalYouTubeChannelIds?.Select(id => new AdditionalYouTubeChannelChange(id)).ToList();
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^UC[A-Za-z0-9_-]{22}$", RegexOptions.CultureInvariant)]
    private static partial Regex ChannelIdPattern();
}
