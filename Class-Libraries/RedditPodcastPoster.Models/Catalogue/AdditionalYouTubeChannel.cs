using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// One of a news organisation's YouTube channels beside its main <see cref="CatalogueIndexingSource.YouTubeChannelId"/>
/// (#1370, JSON <c>indexing.additionalYouTubeChannels[]</c>). The channel is indexed and owned as a whole channel, or,
/// when <see cref="YouTubePlaylistId"/> is set, only through that playlist (which must belong to this channel). The
/// curator sets <see cref="YouTubeChannelId"/> and <see cref="YouTubePlaylistId"/> through the news organisation PATCH;
/// the indexer owns <see cref="YouTubePlaylistQueryIsExpensive"/> and <see cref="YouTubeChannelSearchForbidden"/> for
/// this channel, as it owns the same flags on the main channel. Blank values are read and set as null and never written.
/// </summary>
public sealed class AdditionalYouTubeChannel
{
    private string _youTubeChannelId = string.Empty;
    private string? _youTubePlaylistId;

    [JsonPropertyName("youTubeChannelId")]
    public string YouTubeChannelId
    {
        get => _youTubeChannelId;
        set => _youTubeChannelId = value?.Trim() ?? string.Empty;
    }

    /// <summary>Optional playlist on this channel that scopes the organisation there. Null: the whole channel.</summary>
    [JsonPropertyName("youTubePlaylistId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? YouTubePlaylistId
    {
        get => _youTubePlaylistId;
        set => _youTubePlaylistId = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Indexer-owned: the playlist query on this channel was measured as expensive (same rule as the main channel).</summary>
    [JsonPropertyName("youTubePlaylistQueryIsExpensive")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? YouTubePlaylistQueryIsExpensive { get; set; }

    /// <summary>Indexer-owned: YouTube refused <c>search.list</c> for this channel, so its uploads playlist is read instead.</summary>
    [JsonPropertyName("youTubeChannelSearchForbidden")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? YouTubeChannelSearchForbidden { get; set; }

    /// <summary>True when this channel is claimed whole (no playlist scopes it).</summary>
    [JsonIgnore]
    public bool IsChannelWide => YouTubePlaylistId is null;

    /// <summary>A whole-channel entry for <paramref name="channelId"/>.</summary>
    public static AdditionalYouTubeChannel ChannelWide(string channelId) => new() { YouTubeChannelId = channelId };

    /// <summary>An entry for <paramref name="channelId"/> scoped to <paramref name="playlistId"/> (null or blank: whole channel).</summary>
    public static AdditionalYouTubeChannel ForPlaylist(string channelId, string? playlistId) =>
        new() { YouTubeChannelId = channelId, YouTubePlaylistId = playlistId };

    public AdditionalYouTubeChannel Copy() => new()
    {
        YouTubeChannelId = YouTubeChannelId,
        YouTubePlaylistId = YouTubePlaylistId,
        YouTubePlaylistQueryIsExpensive = YouTubePlaylistQueryIsExpensive,
        YouTubeChannelSearchForbidden = YouTubeChannelSearchForbidden
    };
}
