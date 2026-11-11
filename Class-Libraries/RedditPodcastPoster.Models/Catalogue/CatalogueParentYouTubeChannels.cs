using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// The YouTube channels a news organisation or TV show holds, and the playlist (if any) that scopes it on each: the
/// main <see cref="CatalogueIndexingSource.YouTubeChannelId"/> with <see cref="CatalogueIndexingSource.YouTubePlaylistId"/>,
/// and, for a news organisation only, each of <see cref="NewsOrganisation.AdditionalYouTubeChannels"/> with its own
/// optional playlist (#1370). Shared owner lookup, discovery matching and the channel-wide claim checks read a parent's
/// channels through this, so they treat an additional channel as they treat the main one without the shared
/// <see cref="CatalogueIndexingSource"/> or <see cref="TvShow"/> knowing about additional channels. Built from
/// <see cref="Of(NewsOrganisation)"/> or <see cref="Of(TvShow)"/>; a snapshot of the parent when it was built.
/// </summary>
public sealed class CatalogueParentYouTubeChannels
{
    private readonly string? _mainChannelId;
    private readonly string? _mainPlaylistId;
    private readonly IReadOnlyList<AdditionalYouTubeChannel> _additional;

    private CatalogueParentYouTubeChannels(
        CatalogueIndexingSource? indexing,
        IReadOnlyList<AdditionalYouTubeChannel> additional)
    {
        _mainChannelId = indexing?.YouTubeChannelId;
        _mainPlaylistId = indexing?.YouTubePlaylistId;
        _additional = additional;
    }

    /// <summary>A TV show holds its main channel only.</summary>
    public static CatalogueParentYouTubeChannels Of(TvShow show)
    {
        ArgumentNullException.ThrowIfNull(show);
        return new CatalogueParentYouTubeChannels(show.Indexing, []);
    }

    /// <summary>A news organisation holds its main channel and its additional channels.</summary>
    public static CatalogueParentYouTubeChannels Of(NewsOrganisation organisation)
    {
        ArgumentNullException.ThrowIfNull(organisation);
        return new CatalogueParentYouTubeChannels(
            organisation.Indexing, organisation.AdditionalYouTubeChannels ?? []);
    }

    /// <summary>The main channel (when set) followed by each additional channel, without repeats.</summary>
    public IReadOnlyList<string> All()
    {
        var all = new List<string>();
        if (_mainChannelId is { } main)
        {
            all.Add(main);
        }

        foreach (var additional in _additional)
        {
            if (!all.Contains(additional.YouTubeChannelId, StringComparer.Ordinal))
            {
                all.Add(additional.YouTubeChannelId);
            }
        }

        return all;
    }

    /// <summary>True when <paramref name="channelId"/> is the main or an additional YouTube channel of this parent.</summary>
    public bool Holds(string? channelId) =>
        !string.IsNullOrWhiteSpace(channelId) && All().Contains(channelId.Trim(), StringComparer.Ordinal);

    /// <summary>
    /// The playlist that scopes this parent on <paramref name="channelId"/>: that additional channel's own optional
    /// playlist when it is an additional channel, the main playlist for the main channel, and null (whole channel, or a
    /// channel the parent does not hold) otherwise. The main channel's playlist never scopes an additional channel.
    /// </summary>
    public string? PlaylistIdOn(string? channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return null;
        }

        var trimmed = channelId.Trim();
        var additional = _additional.FirstOrDefault(x =>
            string.Equals(x.YouTubeChannelId, trimmed, StringComparison.Ordinal));
        if (additional != null)
        {
            return additional.YouTubePlaylistId;
        }

        return string.Equals(_mainChannelId, trimmed, StringComparison.Ordinal) ? _mainPlaylistId : null;
    }

    /// <summary>True when this parent claims the whole of <paramref name="channelId"/> (held, and no playlist scopes it there).</summary>
    public bool ClaimsChannelWide(string? channelId) =>
        Holds(channelId) && string.IsNullOrWhiteSpace(PlaylistIdOn(channelId));
}
